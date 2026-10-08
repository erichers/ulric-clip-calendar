using System.Text.Json;
using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Api.Contracts;

public sealed record BrandDto(
    Guid Id,
    string Name,
    string Slug,
    string Color,
    string Description,
    string Instagram,
    string TikTok,
    string YouTube,
    string Facebook,
    string DefaultHashtags,
    string CadenceLabel,
    string CadenceDays,
    string DefaultPostTime,
    double Latitude,
    double Longitude,
    string LocationLabel,
    int ClipCount);

public sealed record CommentDto(Guid Id, string Author, string Body, DateTime CreatedAt);

public sealed record HistoryDto(Guid Id, string FromStatus, string ToStatus, string? Note, string Actor, DateTime CreatedAt);

public sealed record ClipDto(
    Guid Id,
    Guid BrandId,
    string BrandName,
    string BrandColor,
    string Title,
    string Caption,
    string Hashtags,
    string[] Platforms,
    string Series,
    int? SeriesPart,
    string PostDate,
    string PostTime,
    bool StoriesOk,
    string Status,
    string SourceKind,
    string? SourceLink,
    string? OriginalFileName,
    string? ThumbnailUrl,
    string? PreviewUrl,
    double? DurationSeconds,
    int? Width,
    int? Height,
    string? VideoCodec,
    double? TrimStartSeconds,
    double? TrimEndSeconds,
    string MediaState,
    string? MediaMessage,
    CommentDto[] Comments,
    HistoryDto[] History);

public sealed record ShareDto(
    Guid Id,
    string Token,
    string Url,
    Guid? BrandId,
    string? BrandName,
    string? RangeStart,
    string? RangeEnd,
    string Label,
    DateTime CreatedAt);

public sealed record PublicScheduleDto(
    string Label,
    string? BrandName,
    string? BrandColor,
    string? RangeStart,
    string? RangeEnd,
    ClipDto[] Clips);

public sealed record StatusCountDto(string Status, int Count);

public sealed record BrandCountDto(string Brand, string Color, int Count);

public sealed record StatsDto(StatusCountDto[] ByStatus, BrandCountDto[] ByBrand);

public sealed record CapabilitiesDto(bool Ffmpeg, string Message);

public sealed class BrandWrite
{
    public string? Name { get; set; }
    public string? Color { get; set; }
    public string? Description { get; set; }
    public string? Instagram { get; set; }
    public string? TikTok { get; set; }
    public string? YouTube { get; set; }
    public string? Facebook { get; set; }
    public string? DefaultHashtags { get; set; }
    public string? CadenceLabel { get; set; }
    public string? CadenceDays { get; set; }
    public string? DefaultPostTime { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public string? LocationLabel { get; set; }
}

public sealed class StatusRequest
{
    public ApprovalStatus Status { get; set; }
    public string? Actor { get; set; }
    public string? Note { get; set; }
}

public sealed class CommentRequest
{
    public string? Author { get; set; }
    public string? Body { get; set; }
}

public sealed class RescheduleRequest
{
    public string? PostDate { get; set; }
    public string? PostTime { get; set; }
}

public sealed class TrimRequest
{
    public double Start { get; set; }
    public double End { get; set; }
}

public sealed class ShareWrite
{
    public Guid? BrandId { get; set; }
    public string? RangeStart { get; set; }
    public string? RangeEnd { get; set; }
    public string? Label { get; set; }
}

public static class ApiMapper
{
    public static string Camel(Enum value) => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    public static BrandDto ToBrand(Brand brand, int clipCount) => new(
        brand.Id,
        brand.Name,
        brand.Slug,
        brand.Color,
        brand.Description,
        brand.Instagram,
        brand.TikTok,
        brand.YouTube,
        brand.Facebook,
        brand.DefaultHashtags,
        brand.CadenceLabel,
        brand.CadenceDays,
        brand.DefaultPostTime.ToString("HH:mm"),
        brand.Latitude,
        brand.Longitude,
        brand.LocationLabel,
        clipCount);

    public static ClipDto ToClip(Clip clip, string? publicToken, bool includeNotes)
    {
        string? Media(string kind, bool exists) => exists
            ? publicToken is null
                ? $"/api/clips/{clip.Id}/media?kind={kind}"
                : $"/api/public/{publicToken}/media/{clip.Id}?kind={kind}"
            : null;

        var previewExists = !string.IsNullOrWhiteSpace(clip.ProcessedPath) || !string.IsNullOrWhiteSpace(clip.OriginalPath);
        return new ClipDto(
            clip.Id,
            clip.BrandId,
            clip.Brand?.Name ?? "",
            clip.Brand?.Color ?? "#6b8f71",
            clip.Title,
            clip.Caption,
            clip.Hashtags,
            PlatformList.Parse(clip.PlatformsCsv).Select(platform => Camel(platform)).ToArray(),
            clip.Series,
            clip.SeriesPart,
            clip.PostDate.ToString("yyyy-MM-dd"),
            clip.PostTime.ToString("HH:mm:ss"),
            clip.StoriesOk,
            Camel(clip.Status),
            Camel(clip.SourceKind),
            clip.SourceLink,
            clip.OriginalFileName,
            Media("thumbnail", !string.IsNullOrWhiteSpace(clip.ThumbnailPath)),
            Media("preview", previewExists),
            clip.DurationSeconds,
            clip.Width,
            clip.Height,
            clip.VideoCodec,
            clip.TrimStartSeconds,
            clip.TrimEndSeconds,
            Camel(clip.MediaState),
            clip.MediaMessage,
            includeNotes
                ? clip.Comments.OrderBy(comment => comment.CreatedAt).Select(comment => new CommentDto(comment.Id, comment.Author, comment.Body, comment.CreatedAt)).ToArray()
                : Array.Empty<CommentDto>(),
            includeNotes
                ? clip.History.OrderBy(change => change.CreatedAt).Select(change => new HistoryDto(change.Id, Camel(change.FromStatus), Camel(change.ToStatus), change.Note, change.Actor, change.CreatedAt)).ToArray()
                : Array.Empty<HistoryDto>());
    }

    public static ShareDto ToShare(ShareLink link) => new(
        link.Id,
        link.Token,
        $"/s/{link.Token}",
        link.BrandId,
        link.Brand?.Name,
        link.RangeStart?.ToString("yyyy-MM-dd"),
        link.RangeEnd?.ToString("yyyy-MM-dd"),
        link.Label,
        link.CreatedAt);
}
