namespace Ulric.ClipCalendar.Api.Domain;

public sealed class ClipInput
{
    public Guid BrandId { get; set; }
    public string? Title { get; set; }
    public string? Caption { get; set; }
    public string? Hashtags { get; set; }
    public List<Platform>? Platforms { get; set; }
    public string? Series { get; set; }
    public int? SeriesPart { get; set; }
    public string? PostDate { get; set; }
    public string? PostTime { get; set; }
    public bool StoriesOk { get; set; }
    public string? SourceLink { get; set; }
    public ApprovalStatus? Status { get; set; }
    public string? Actor { get; set; }
}

public sealed class ClipParsed
{
    public required string Title { get; init; }
    public required string Caption { get; init; }
    public required string Hashtags { get; init; }
    public required List<Platform> Platforms { get; init; }
    public required string Series { get; init; }
    public int? SeriesPart { get; init; }
    public DateOnly PostDate { get; init; }
    public string? PostTimeText { get; init; }
    public bool StoriesOk { get; init; }
    public string? SourceLink { get; init; }
    public ApprovalStatus? Status { get; init; }
}

public static class ClipRules
{
    public static ClipParsed Parse(ClipInput input)
    {
        var errors = new List<string>();
        var title = (input.Title ?? "").Trim();
        var caption = (input.Caption ?? "").Trim();
        var hashtags = (input.Hashtags ?? "").Trim();
        var series = (input.Series ?? "").Trim();
        var link = string.IsNullOrWhiteSpace(input.SourceLink) ? null : input.SourceLink.Trim();

        if (title.Length == 0)
        {
            errors.Add("A title is required.");
        }
        else if (title.Length > 160)
        {
            errors.Add("Title must be 160 characters or fewer.");
        }

        if (caption.Length > 2200)
        {
            errors.Add("Caption must be 2200 characters or fewer.");
        }

        if (hashtags.Length > 500)
        {
            errors.Add("Hashtags must be 500 characters or fewer.");
        }

        if (series.Length > 120)
        {
            errors.Add("Series must be 120 characters or fewer.");
        }

        if (input.SeriesPart is < 1)
        {
            errors.Add("Series part starts at 1.");
        }

        if (input.BrandId == Guid.Empty)
        {
            errors.Add("Choose a brand.");
        }

        var platforms = (input.Platforms ?? new List<Platform>()).Distinct().ToList();
        if (platforms.Count == 0)
        {
            errors.Add("Choose at least one platform.");
        }

        if (!ScheduleRules.TryParseDate(input.PostDate, out var postDate))
        {
            errors.Add("Post date should look like 2026-10-08.");
            postDate = default;
        }

        if (!string.IsNullOrWhiteSpace(input.PostTime) && !ScheduleRules.TryParseTime(input.PostTime, out _))
        {
            errors.Add("Post time should look like 11:00.");
        }

        if (link is not null && !IsHttp(link))
        {
            errors.Add("Clip links must start with http:// or https://.");
        }

        if (errors.Count > 0)
        {
            throw new ClipValidationException(errors);
        }

        return new ClipParsed
        {
            Title = title,
            Caption = caption,
            Hashtags = hashtags,
            Platforms = platforms,
            Series = series,
            SeriesPart = input.SeriesPart,
            PostDate = postDate,
            PostTimeText = string.IsNullOrWhiteSpace(input.PostTime) ? null : input.PostTime.Trim(),
            StoriesOk = input.StoriesOk,
            SourceLink = link,
            Status = input.Status
        };
    }

    private static bool IsHttp(string link) =>
        Uri.TryCreate(link, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}

public static class Slugs
{
    public static string Make(string name)
    {
        var chars = name.Trim().ToLowerInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
        var slug = new string(chars);
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        slug = slug.Trim('-');
        return slug.Length == 0 ? "brand" : slug;
    }
}
