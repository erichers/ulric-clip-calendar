using System.Globalization;

namespace Ulric.ClipCalendar.Api.Domain;

public static class ScheduleRules
{
    public static void Move(Clip clip, DateOnly postDate, TimeOnly? postTime, DateTime utcNow)
    {
        var earliest = DateOnly.FromDateTime(utcNow).AddYears(-2);
        var latest = DateOnly.FromDateTime(utcNow).AddYears(3);
        if (postDate < earliest || postDate > latest)
        {
            throw new ArgumentOutOfRangeException(nameof(postDate), "Pick a post date within two years back or three years ahead.");
        }

        clip.PostDate = postDate;
        if (postTime is TimeOnly time)
        {
            clip.PostTime = time;
        }

        clip.UpdatedAt = utcNow;
    }

    public static IReadOnlyList<Clip> InRange(IEnumerable<Clip> clips, DateOnly start, DateOnly end, Guid? brandId)
    {
        if (end < start)
        {
            throw new ArgumentException("Range end is before the start.");
        }

        return clips
            .Where(clip => clip.PostDate >= start && clip.PostDate <= end)
            .Where(clip => brandId is null || clip.BrandId == brandId)
            .OrderBy(clip => clip.PostDate)
            .ThenBy(clip => clip.PostTime)
            .ThenBy(clip => clip.Title, StringComparer.Ordinal)
            .ToList();
    }

    public static (DateOnly Start, DateOnly End) MonthGrid(int year, int month)
    {
        var first = new DateOnly(year, month, 1);
        var delta = ((int)first.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = first.AddDays(-delta);
        return (start, start.AddDays(41));
    }

    public static (DateOnly Start, DateOnly End) WeekRange(DateOnly day)
    {
        var delta = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var start = day.AddDays(-delta);
        return (start, start.AddDays(6));
    }

    public static bool TryParseDate(string? text, out DateOnly date) =>
        DateOnly.TryParseExact(text?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    public static bool TryParseTime(string? text, out TimeOnly time)
    {
        var value = text?.Trim() ?? "";
        if (TimeOnly.TryParseExact(value, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out time))
        {
            return true;
        }

        return TimeOnly.TryParseExact(value, "HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out time);
    }
}

public static class Weekdays
{
    public static string English(DateOnly date) => date.DayOfWeek switch
    {
        DayOfWeek.Monday => "Monday",
        DayOfWeek.Tuesday => "Tuesday",
        DayOfWeek.Wednesday => "Wednesday",
        DayOfWeek.Thursday => "Thursday",
        DayOfWeek.Friday => "Friday",
        DayOfWeek.Saturday => "Saturday",
        DayOfWeek.Sunday => "Sunday",
        _ => ""
    };
}
