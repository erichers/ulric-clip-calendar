using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Tests;

public class ScheduleRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Month_grid_starts_on_monday()
    {
        var (start, end) = ScheduleRules.MonthGrid(2026, 10);
        Assert.Equal(new DateOnly(2026, 9, 28), start);
        Assert.Equal(new DateOnly(2026, 11, 8), end);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
        Assert.Equal(DayOfWeek.Sunday, end.DayOfWeek);
    }

    [Fact]
    public void Week_range_starts_on_monday_for_a_sunday()
    {
        var (start, end) = ScheduleRules.WeekRange(new DateOnly(2026, 10, 11));
        Assert.Equal(new DateOnly(2026, 10, 5), start);
        Assert.Equal(new DateOnly(2026, 10, 11), end);
        Assert.Equal(DayOfWeek.Monday, start.DayOfWeek);
    }

    [Fact]
    public void Move_updates_date_and_optional_time()
    {
        var clip = new Clip { Title = "Pour", PostDate = new DateOnly(2026, 10, 1), PostTime = new TimeOnly(6, 30) };
        ScheduleRules.Move(clip, new DateOnly(2026, 10, 12), new TimeOnly(7, 15), Now);
        Assert.Equal(new DateOnly(2026, 10, 12), clip.PostDate);
        Assert.Equal(new TimeOnly(7, 15), clip.PostTime);

        ScheduleRules.Move(clip, new DateOnly(2026, 10, 13), null, Now);
        Assert.Equal(new DateOnly(2026, 10, 13), clip.PostDate);
        Assert.Equal(new TimeOnly(7, 15), clip.PostTime);
    }

    [Fact]
    public void Move_rejects_dates_far_outside_the_studio_window()
    {
        var clip = new Clip();
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            ScheduleRules.Move(clip, new DateOnly(2010, 1, 1), null, Now));
    }

    [Fact]
    public void In_range_filters_brand_and_orders_by_slot()
    {
        var fern = Guid.NewGuid();
        var night = Guid.NewGuid();
        var clips = new[]
        {
            new Clip { BrandId = night, Title = "Late", PostDate = new DateOnly(2026, 10, 8), PostTime = new TimeOnly(21, 0) },
            new Clip { BrandId = fern, Title = "Dawn", PostDate = new DateOnly(2026, 10, 8), PostTime = new TimeOnly(6, 30) },
            new Clip { BrandId = fern, Title = "Next", PostDate = new DateOnly(2026, 10, 9), PostTime = new TimeOnly(11, 0) },
            new Clip { BrandId = fern, Title = "Old", PostDate = new DateOnly(2026, 9, 1), PostTime = new TimeOnly(11, 0) }
        };

        var visible = ScheduleRules.InRange(clips, new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 8), fern);
        Assert.Equal(new[] { "Dawn" }, visible.Select(clip => clip.Title).ToArray());

        var both = ScheduleRules.InRange(clips, new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9), null);
        Assert.Equal(new[] { "Dawn", "Late", "Next" }, both.Select(clip => clip.Title).ToArray());
    }

    [Fact]
    public void Inverted_range_is_rejected()
    {
        Assert.Throws<ArgumentException>(() =>
            ScheduleRules.InRange(Array.Empty<Clip>(), new DateOnly(2026, 10, 10), new DateOnly(2026, 10, 1), null));
    }
}
