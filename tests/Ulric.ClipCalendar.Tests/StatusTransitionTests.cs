using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Tests;

public class StatusTransitionTests
{
    private static readonly DateTime Now = new(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc);

    [Theory]
    [InlineData(ApprovalStatus.Draft, ApprovalStatus.NeedsReview, true)]
    [InlineData(ApprovalStatus.Draft, ApprovalStatus.Approved, false)]
    [InlineData(ApprovalStatus.NeedsReview, ApprovalStatus.Approved, true)]
    [InlineData(ApprovalStatus.NeedsReview, ApprovalStatus.Hold, true)]
    [InlineData(ApprovalStatus.NeedsReview, ApprovalStatus.Draft, true)]
    [InlineData(ApprovalStatus.Approved, ApprovalStatus.Hold, true)]
    [InlineData(ApprovalStatus.Approved, ApprovalStatus.Draft, false)]
    [InlineData(ApprovalStatus.Approved, ApprovalStatus.NeedsReview, true)]
    [InlineData(ApprovalStatus.Hold, ApprovalStatus.Draft, true)]
    [InlineData(ApprovalStatus.Hold, ApprovalStatus.NeedsReview, true)]
    [InlineData(ApprovalStatus.Hold, ApprovalStatus.Approved, false)]
    public void Allowed_edges_match_the_review_rules(ApprovalStatus from, ApprovalStatus to, bool allowed)
    {
        Assert.Equal(allowed, StatusMachine.CanTransition(from, to));
    }

    [Fact]
    public void Apply_records_history_and_keeps_the_previous_status_on_failure()
    {
        var clip = new Clip { Id = Guid.NewGuid(), Status = ApprovalStatus.Draft };
        var change = StatusChanges.Apply(clip, ApprovalStatus.NeedsReview, "  Avery Chen  ", "  Ready for a look.  ", Now);

        Assert.NotNull(change);
        Assert.Equal(ApprovalStatus.NeedsReview, clip.Status);
        Assert.Equal(ApprovalStatus.Draft, change!.FromStatus);
        Assert.Equal("Avery Chen", change.Actor);
        Assert.Equal("Ready for a look.", change.Note);
        Assert.Equal(Now, clip.UpdatedAt);

        StatusChanges.Apply(clip, ApprovalStatus.Approved, "Jules Okonkwo", null, Now);
        Assert.Throws<InvalidStatusTransitionException>(() =>
            StatusChanges.Apply(clip, ApprovalStatus.Draft, "Jules Okonkwo", null, Now));
        Assert.Equal(ApprovalStatus.Approved, clip.Status);
    }

    [Fact]
    public void Same_status_does_not_write_another_history_row()
    {
        var clip = new Clip { Id = Guid.NewGuid(), Status = ApprovalStatus.Hold };
        var change = StatusChanges.Apply(clip, ApprovalStatus.Hold, "", " ", Now);
        Assert.Null(change);
        Assert.Equal(ApprovalStatus.Hold, clip.Status);
    }

    [Fact]
    public void Blank_actor_falls_back_to_studio()
    {
        var clip = new Clip { Id = Guid.NewGuid(), Status = ApprovalStatus.Hold };
        var change = StatusChanges.Apply(clip, ApprovalStatus.Draft, "   ", null, Now);
        Assert.Equal("Studio", change!.Actor);
        Assert.Null(change.Note);
    }

    [Fact]
    public void Review_queue_puts_needs_review_ahead_of_drafts_and_holds()
    {
        Assert.True(ReviewOrder.Rank(ApprovalStatus.NeedsReview) < ReviewOrder.Rank(ApprovalStatus.Draft));
        Assert.True(ReviewOrder.Rank(ApprovalStatus.Draft) < ReviewOrder.Rank(ApprovalStatus.Hold));
        Assert.True(ReviewOrder.Rank(ApprovalStatus.Hold) < ReviewOrder.Rank(ApprovalStatus.Approved));
    }

    [Fact]
    public void Parse_rejects_a_clip_with_no_platform_and_accepts_a_complete_one()
    {
        var bad = new ClipInput { Title = "Cup", BrandId = Guid.NewGuid(), PostDate = "2026-10-08", Platforms = new List<Platform>() };
        var error = Assert.Throws<ClipValidationException>(() => ClipRules.Parse(bad));
        Assert.Contains("platform", error.Message, StringComparison.OrdinalIgnoreCase);

        var good = ClipRules.Parse(new ClipInput
        {
            Title = "Cup",
            BrandId = Guid.NewGuid(),
            PostDate = "2026-10-08",
            PostTime = "06:30",
            Platforms = new List<Platform> { Platform.TikTok, Platform.TikTok },
            SourceLink = "https://videos.example.com/nightshift/bell",
            SeriesPart = 2
        });
        Assert.Equal(new[] { Platform.TikTok }, good.Platforms);
        Assert.Equal("https://videos.example.com/nightshift/bell", good.SourceLink);
    }
}
