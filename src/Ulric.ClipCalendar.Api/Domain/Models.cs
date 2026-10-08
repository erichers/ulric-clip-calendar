namespace Ulric.ClipCalendar.Api.Domain;

public enum ApprovalStatus
{
    Draft,
    NeedsReview,
    Approved,
    Hold
}

public enum Platform
{
    InstagramReels,
    TikTok,
    YouTubeShorts,
    Facebook
}

public enum ClipSourceKind
{
    Upload,
    Link
}

public enum MediaJobState
{
    NotRequested,
    Pending,
    Running,
    Succeeded,
    Failed,
    Unavailable
}

public sealed class Brand
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Color { get; set; } = "#6b8f71";
    public string Description { get; set; } = "";
    public string Instagram { get; set; } = "";
    public string TikTok { get; set; } = "";
    public string YouTube { get; set; } = "";
    public string Facebook { get; set; } = "";
    public string DefaultHashtags { get; set; } = "";
    public string CadenceLabel { get; set; } = "";
    public string CadenceDays { get; set; } = "";
    public TimeOnly DefaultPostTime { get; set; } = new(11, 0);
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string LocationLabel { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<Clip> Clips { get; set; } = new();
}

public sealed class Clip
{
    public Guid Id { get; set; }
    public Guid BrandId { get; set; }
    public Brand? Brand { get; set; }
    public string Title { get; set; } = "";
    public string Caption { get; set; } = "";
    public string Hashtags { get; set; } = "";
    public string PlatformsCsv { get; set; } = "";
    public string Series { get; set; } = "";
    public int? SeriesPart { get; set; }
    public DateOnly PostDate { get; set; }
    public TimeOnly PostTime { get; set; }
    public bool StoriesOk { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Draft;
    public ClipSourceKind SourceKind { get; set; } = ClipSourceKind.Upload;
    public string? SourceLink { get; set; }
    public string? OriginalFileName { get; set; }
    public string? OriginalPath { get; set; }
    public string? ProcessedPath { get; set; }
    public string? ThumbnailPath { get; set; }
    public double? DurationSeconds { get; set; }
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? VideoCodec { get; set; }
    public double? TrimStartSeconds { get; set; }
    public double? TrimEndSeconds { get; set; }
    public MediaJobState MediaState { get; set; } = MediaJobState.NotRequested;
    public string? MediaMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<ClipComment> Comments { get; set; } = new();
    public List<StatusEvent> History { get; set; } = new();
}

public sealed class ClipComment
{
    public Guid Id { get; set; }
    public Guid ClipId { get; set; }
    public Clip? Clip { get; set; }
    public string Author { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class StatusEvent
{
    public Guid Id { get; set; }
    public Guid ClipId { get; set; }
    public Clip? Clip { get; set; }
    public ApprovalStatus FromStatus { get; set; }
    public ApprovalStatus ToStatus { get; set; }
    public string? Note { get; set; }
    public string Actor { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public sealed class ShareLink
{
    public Guid Id { get; set; }
    public string Token { get; set; } = "";
    public Guid? BrandId { get; set; }
    public Brand? Brand { get; set; }
    public DateOnly? RangeStart { get; set; }
    public DateOnly? RangeEnd { get; set; }
    public string Label { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}

public static class PlatformList
{
    public static string Format(IEnumerable<Platform> platforms) =>
        string.Join(',', platforms.Distinct());

    public static List<Platform> Parse(string? text)
    {
        var list = new List<Platform>();
        if (string.IsNullOrWhiteSpace(text))
        {
            return list;
        }

        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (Enum.TryParse<Platform>(part, ignoreCase: true, out var value))
            {
                list.Add(value);
            }
        }

        return list;
    }
}

public sealed class ClipValidationException : Exception
{
    public ClipValidationException(IReadOnlyList<string> errors) : base(errors[0])
    {
        Errors = errors;
    }

    public IReadOnlyList<string> Errors { get; }
}

public sealed class InvalidStatusTransitionException : Exception
{
    public InvalidStatusTransitionException(ApprovalStatus from, ApprovalStatus to)
        : base($"Cannot move a clip from {StatusLabels.For(from)} to {StatusLabels.For(to)}.")
    {
        From = from;
        To = to;
    }

    public ApprovalStatus From { get; }
    public ApprovalStatus To { get; }
}
