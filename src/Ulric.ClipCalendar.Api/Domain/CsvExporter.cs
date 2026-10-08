using System.Globalization;
using System.Text;

namespace Ulric.ClipCalendar.Api.Domain;

public sealed record ClipExportRow(
    DateOnly PostDate,
    string Brand,
    string Series,
    int? SeriesPart,
    IReadOnlyList<Platform> Platforms,
    string Caption,
    string Hashtags,
    ApprovalStatus Status,
    string ClipUrl);

public static class CsvExporter
{
    public const string Header = "post_date,weekday,brand,series,series_part,platform,caption,hashtags,status,clip_url";

    public static string Export(IEnumerable<ClipExportRow> rows)
    {
        var builder = new StringBuilder();
        builder.Append(Header);
        builder.Append('\n');
        foreach (var row in rows)
        {
            builder.Append(Field(row.PostDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
            builder.Append(',');
            builder.Append(Field(Weekdays.English(row.PostDate)));
            builder.Append(',');
            builder.Append(Field(row.Brand));
            builder.Append(',');
            builder.Append(Field(row.Series));
            builder.Append(',');
            builder.Append(Field(row.SeriesPart?.ToString(CultureInfo.InvariantCulture) ?? ""));
            builder.Append(',');
            builder.Append(Field(PlatformLabel(row.Platforms)));
            builder.Append(',');
            builder.Append(Field(row.Caption));
            builder.Append(',');
            builder.Append(Field(row.Hashtags));
            builder.Append(',');
            builder.Append(Field(StatusLabels.For(row.Status)));
            builder.Append(',');
            builder.Append(Field(row.ClipUrl));
            builder.Append('\n');
        }

        return builder.ToString();
    }

    public static IReadOnlyList<ClipExportRow> Rows(IEnumerable<Clip> clips, string? publicBaseUrl)
    {
        return clips.Select(clip => new ClipExportRow(
            clip.PostDate,
            clip.Brand?.Name ?? "",
            clip.Series,
            clip.SeriesPart,
            PlatformList.Parse(clip.PlatformsCsv),
            clip.Caption,
            clip.Hashtags,
            clip.Status,
            ClipUrl(clip, publicBaseUrl))).ToList();
    }

    public static string ClipUrl(Clip clip, string? publicBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(clip.SourceLink))
        {
            return clip.SourceLink;
        }

        var path = $"/api/clips/{clip.Id}/media?kind=preview";
        if (string.IsNullOrWhiteSpace(publicBaseUrl))
        {
            return path;
        }

        return publicBaseUrl.TrimEnd('/') + path;
    }

    public static string PlatformLabel(IEnumerable<Platform> platforms) =>
        string.Join("; ", platforms.Select(Label));

    public static string Label(Platform platform) => platform switch
    {
        Platform.InstagramReels => "IG Reels",
        Platform.TikTok => "TikTok",
        Platform.YouTubeShorts => "YT Shorts",
        Platform.Facebook => "FB",
        _ => platform.ToString()
    };

    public static string Field(string? value)
    {
        var text = value ?? "";
        if (text.Length > 0 && (text[0] == '=' || text[0] == '+' || text[0] == '-' || text[0] == '@'))
        {
            text = "'" + text;
        }

        if (text.Contains('"') || text.Contains(',') || text.Contains('\n') || text.Contains('\r'))
        {
            return "\"" + text.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return text;
    }
}
