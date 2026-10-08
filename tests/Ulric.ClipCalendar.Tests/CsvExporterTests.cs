using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Tests;

public class CsvExporterTests
{
    [Fact]
    public void Export_uses_the_schedule_columns_and_escapes_caption_text()
    {
        var row = new ClipExportRow(
            new DateOnly(2026, 10, 8),
            "Fern & Field",
            "Spore Notes",
            2,
            new[] { Platform.InstagramReels, Platform.TikTok },
            "Hello, \"world\"",
            "#fern #field",
            ApprovalStatus.NeedsReview,
            "https://videos.example.com/fern/a");

        var lines = CsvExporter.Export(new[] { row }).TrimEnd('\n').Split('\n');

        Assert.Equal(
            "post_date,weekday,brand,series,series_part,platform,caption,hashtags,status,clip_url",
            lines[0]);
        Assert.Equal(
            "2026-10-08,Thursday,Fern & Field,Spore Notes,2,IG Reels; TikTok,\"Hello, \"\"world\"\"\",#fern #field,needs review,https://videos.example.com/fern/a",
            lines[1]);
    }

    [Fact]
    public void Export_neutralizes_formula_cells_and_leaves_missing_parts_blank()
    {
        var row = new ClipExportRow(
            new DateOnly(2026, 10, 5),
            "Night Shift Coffee",
            "",
            null,
            new[] { Platform.Facebook },
            "=SUM(1)",
            "",
            ApprovalStatus.Draft,
            "/api/clips/abc/media?kind=preview");

        var line = CsvExporter.Export(new[] { row }).TrimEnd('\n').Split('\n')[1];
        Assert.Equal(
            "2026-10-05,Monday,Night Shift Coffee,,,FB,'=SUM(1),,draft,/api/clips/abc/media?kind=preview",
            line);
    }

    [Fact]
    public void Rows_prefer_a_source_link_and_can_prefix_a_public_base_url()
    {
        var linked = new Clip
        {
            Id = Guid.NewGuid(),
            SourceLink = "https://videos.example.com/nightshift/bell",
            Brand = new Brand { Name = "Night Shift Coffee" },
            PostDate = new DateOnly(2026, 10, 9)
        };
        var uploaded = new Clip
        {
            Id = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee"),
            Brand = new Brand { Name = "Fern & Field" },
            PostDate = new DateOnly(2026, 10, 8)
        };

        var rows = CsvExporter.Rows(new[] { linked, uploaded }, "http://localhost:5080");
        Assert.Equal("https://videos.example.com/nightshift/bell", rows[0].ClipUrl);
        Assert.Equal(
            "http://localhost:5080/api/clips/aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee/media?kind=preview",
            rows[1].ClipUrl);
    }
}
