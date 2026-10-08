using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Ulric.ClipCalendar.Api.Domain;

public static partial class ShareTokens
{
    public static string Create()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static bool LooksValid(string? token) =>
        !string.IsNullOrWhiteSpace(token) && TokenPattern().IsMatch(token);

    [GeneratedRegex("^[A-Za-z0-9_-]{43}$")]
    private static partial Regex TokenPattern();
}

public static class ShareScope
{
    public static IReadOnlyList<Clip> Visible(ShareLink link, IEnumerable<Clip> clips)
    {
        IEnumerable<Clip> query = clips;
        if (link.BrandId is Guid brandId)
        {
            query = query.Where(clip => clip.BrandId == brandId);
        }

        if (link.RangeStart is DateOnly start)
        {
            query = query.Where(clip => clip.PostDate >= start);
        }

        if (link.RangeEnd is DateOnly end)
        {
            query = query.Where(clip => clip.PostDate <= end);
        }

        return query
            .OrderBy(clip => clip.PostDate)
            .ThenBy(clip => clip.PostTime)
            .ThenBy(clip => clip.Title, StringComparer.Ordinal)
            .ToList();
    }
}
