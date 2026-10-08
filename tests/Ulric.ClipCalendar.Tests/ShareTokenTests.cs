using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Ulric.ClipCalendar.Api.Contracts;
using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Tests;

public class ShareTokenTests
{
    [Fact]
    public void Tokens_are_url_safe_and_unguessable_length()
    {
        var first = ShareTokens.Create();
        var second = ShareTokens.Create();
        Assert.Matches("^[A-Za-z0-9_-]{43}$", first);
        Assert.NotEqual(first, second);
        Assert.True(ShareTokens.LooksValid(first));
        Assert.False(ShareTokens.LooksValid("short"));
    }

    [Fact]
    public void Scope_keeps_other_brands_and_dates_out_and_includes_the_edges()
    {
        var fern = Guid.NewGuid();
        var night = Guid.NewGuid();
        var clips = new[]
        {
            new Clip { BrandId = fern, Title = "A", PostDate = new DateOnly(2026, 10, 1), PostTime = new TimeOnly(11, 0) },
            new Clip { BrandId = fern, Title = "B", PostDate = new DateOnly(2026, 10, 15), PostTime = new TimeOnly(11, 0) },
            new Clip { BrandId = night, Title = "C", PostDate = new DateOnly(2026, 10, 15), PostTime = new TimeOnly(6, 30) }
        };

        var brandAndRange = new ShareLink
        {
            BrandId = fern,
            RangeStart = new DateOnly(2026, 10, 10),
            RangeEnd = new DateOnly(2026, 10, 20)
        };
        Assert.Equal(new[] { "B" }, ShareScope.Visible(brandAndRange, clips).Select(clip => clip.Title).ToArray());

        var oneDay = new ShareLink
        {
            RangeStart = new DateOnly(2026, 10, 15),
            RangeEnd = new DateOnly(2026, 10, 15)
        };
        Assert.Equal(new[] { "C", "B" }, ShareScope.Visible(oneDay, clips).Select(clip => clip.Title).ToArray());

        var fernOnly = new ShareLink { BrandId = fern };
        Assert.Equal(2, ShareScope.Visible(fernOnly, clips).Count);
    }

    [Fact]
    public async Task Public_route_returns_only_the_shared_slice_and_hides_unknown_tokens()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var fern = await ApiClient.CreateBrand(client, "Fern Test");
        var night = await ApiClient.CreateBrand(client, "Night Test");
        await ApiClient.CreateClip(client, fern, "Inside", "2026-10-08");
        await ApiClient.CreateClip(client, night, "Outside", "2026-10-08");
        await ApiClient.CreateClip(client, fern, "Too early", "2026-09-01");

        var share = await client.PostAsJsonAsync("/api/shares", new
        {
            brandId = fern,
            rangeStart = "2026-10-01",
            rangeEnd = "2026-10-31",
            label = "October fern"
        });
        var created = await ApiClient.Read<ShareDto>(share);

        var ok = await client.GetAsync($"/api/public/{created.Token}");
        var body = await ApiClient.Read<PublicScheduleDto>(ok);
        Assert.Equal(new[] { "Inside" }, body.Clips.Select(clip => clip.Title).ToArray());
        Assert.Empty(body.Clips[0].Comments);

        var missing = await client.GetAsync("/api/public/not-a-real-token-value");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);

        var outsideId = body.Clips[0].Id;
        var hidden = await client.GetAsync($"/api/public/not-a-real-token-value/media/{outsideId}");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
    }
}

public class ApiFlowTests
{
    [Fact]
    public async Task Reschedule_moves_a_clip_and_range_filters_follow()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var brand = await ApiClient.CreateBrand(client, "Fern Flow");
        var clipId = await ApiClient.CreateClip(client, brand, "Broth", "2026-10-08");

        var moved = await client.PostAsJsonAsync($"/api/clips/{clipId}/reschedule", new
        {
            postDate = "2026-10-12",
            postTime = "18:45"
        });
        var clip = await ApiClient.Read<ClipDto>(moved);
        Assert.Equal("2026-10-12", clip.PostDate);
        Assert.Equal("18:45:00", clip.PostTime);

        var inRange = await client.GetFromJsonAsync<ClipDto[]>("/api/clips?from=2026-10-12&to=2026-10-12");
        Assert.Contains(inRange!, item => item.Id == clipId);
        var outOfRange = await client.GetFromJsonAsync<ClipDto[]>("/api/clips?from=2026-10-01&to=2026-10-02");
        Assert.DoesNotContain(outOfRange!, item => item.Id == clipId);

        var tooFar = await client.PostAsJsonAsync($"/api/clips/{clipId}/reschedule", new { postDate = "2010-01-01" });
        Assert.Equal(HttpStatusCode.BadRequest, tooFar.StatusCode);
    }

    [Fact]
    public async Task Illegal_status_change_is_rejected_and_a_legal_one_is_stored()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var brand = await ApiClient.CreateBrand(client, "Night Flow");
        var clipId = await ApiClient.CreateClip(client, brand, "First tray", "2026-10-08");

        var denied = await client.PostAsJsonAsync($"/api/clips/{clipId}/status", new
        {
            status = "approved",
            actor = "Jules Okonkwo",
            note = "Too soon"
        });
        Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        var still = await client.GetFromJsonAsync<ClipDto>($"/api/clips/{clipId}");
        Assert.Equal("draft", still!.Status);

        var review = await client.PostAsJsonAsync($"/api/clips/{clipId}/status", new
        {
            status = "needsReview",
            actor = "Avery Chen",
            note = "Ready for a look"
        });
        var reviewed = await ApiClient.Read<ClipDto>(review);
        Assert.Equal("needsReview", reviewed.Status);
        Assert.Single(reviewed.History);
        Assert.Equal("Avery Chen", reviewed.History[0].Actor);

        var comment = await client.PostAsJsonAsync($"/api/clips/{clipId}/comments", new
        {
            author = "Jules Okonkwo",
            body = "Soften the last line."
        });
        var withComment = await ApiClient.Read<ClipDto>(comment);
        Assert.Equal("Soften the last line.", withComment.Comments[0].Body);
    }

    [Fact]
    public async Task Csv_and_pdf_exports_honor_the_brand_filter()
    {
        await using var factory = new ApiFactory();
        var client = factory.CreateClient();
        var fern = await ApiClient.CreateBrand(client, "Fern Export");
        var night = await ApiClient.CreateBrand(client, "Night Export");
        await ApiClient.CreateClip(client, fern, "Fern row", "2026-10-08", "Hello, export");
        await ApiClient.CreateClip(client, night, "Night row", "2026-10-08");

        var csvResponse = await client.GetAsync($"/api/export/csv?brandId={fern}&from=2026-10-01&to=2026-10-31");
        var csv = await ApiClient.Text(csvResponse);
        var lines = csv.TrimEnd('\n').Split('\n');
        Assert.Equal(CsvExporter.Header, lines[0]);
        Assert.Contains("Fern Export", lines[1], StringComparison.Ordinal);
        Assert.Contains("\"Hello, export\"", lines[1], StringComparison.Ordinal);
        Assert.DoesNotContain("Night Export", csv, StringComparison.Ordinal);
        Assert.Equal("attachment", csvResponse.Content.Headers.ContentDisposition?.DispositionType);

        var pdfResponse = await client.GetAsync($"/api/export/pdf?brandId={fern}");
        var bytes = await pdfResponse.Content.ReadAsByteArrayAsync();
        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal((byte)'%', bytes[0]);
        Assert.Equal((byte)'P', bytes[1]);
        Assert.Equal((byte)'D', bytes[2]);
        Assert.Equal((byte)'F', bytes[3]);
    }
}

public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly string databasePath = Path.Combine(Path.GetTempPath(), $"ulric-test-{Guid.NewGuid():N}.db");
    private readonly string storagePath = Path.Combine(Path.GetTempPath(), $"ulric-store-{Guid.NewGuid():N}");

    public ApiFactory()
    {
        Directory.CreateDirectory(storagePath);
    }

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Default", $"Data Source={databasePath}");
        builder.UseSetting("Storage:Root", storagePath);
        builder.UseSetting("Seed:Enabled", "false");
        builder.UseSetting("environment", "Testing");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = $"Data Source={databasePath}",
                ["Storage:Root"] = storagePath,
                ["Seed:Enabled"] = "false"
            });
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        TryDeleteFile(databasePath);
        TryDeleteDirectory(storagePath);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch (IOException)
        {
        }
    }
}

public static class ApiClient
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static async Task<Guid> CreateBrand(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/api/brands", new { name, color = "#3f6b4e" });
        var brand = await Read<BrandDto>(response);
        return brand.Id;
    }

    public static async Task<Guid> CreateClip(HttpClient client, Guid brandId, string title, string postDate, string? caption = null)
    {
        var response = await client.PostAsJsonAsync("/api/clips", new
        {
            brandId,
            title,
            caption = caption ?? "A test caption",
            hashtags = "#test",
            platforms = new[] { "instagramReels" },
            series = "Notes",
            seriesPart = 1,
            postDate,
            postTime = "11:00",
            storiesOk = false,
            status = "draft"
        });
        var clip = await Read<ClipDto>(response);
        return clip.Id;
    }

    public static async Task<T> Read<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{(int)response.StatusCode} {response.StatusCode}: {body}");
        }

        return JsonSerializer.Deserialize<T>(body, Json)!;
    }

    public static async Task<string> Text(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"{(int)response.StatusCode}: {body}");
        }

        return body;
    }
}
