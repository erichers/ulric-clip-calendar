namespace Ulric.ClipCalendar.Api.Domain;

public static class StatusLabels
{
    public static string For(ApprovalStatus status) => status switch
    {
        ApprovalStatus.NeedsReview => "needs review",
        ApprovalStatus.Draft => "draft",
        ApprovalStatus.Approved => "approved",
        ApprovalStatus.Hold => "hold",
        _ => status.ToString()
    };
}

public static class StatusMachine
{
    private static readonly IReadOnlyDictionary<ApprovalStatus, ApprovalStatus[]> Allowed =
        new Dictionary<ApprovalStatus, ApprovalStatus[]>
        {
            [ApprovalStatus.Draft] = new[] { ApprovalStatus.NeedsReview, ApprovalStatus.Hold },
            [ApprovalStatus.NeedsReview] = new[] { ApprovalStatus.Approved, ApprovalStatus.Hold, ApprovalStatus.Draft },
            [ApprovalStatus.Approved] = new[] { ApprovalStatus.Hold, ApprovalStatus.NeedsReview },
            [ApprovalStatus.Hold] = new[] { ApprovalStatus.Draft, ApprovalStatus.NeedsReview }
        };

    public static bool CanTransition(ApprovalStatus from, ApprovalStatus to)
    {
        if (from == to)
        {
            return true;
        }

        return Allowed.TryGetValue(from, out var next) && next.Contains(to);
    }

    public static void Ensure(ApprovalStatus from, ApprovalStatus to)
    {
        if (!CanTransition(from, to))
        {
            throw new InvalidStatusTransitionException(from, to);
        }
    }

    public static IReadOnlyList<ApprovalStatus> Next(ApprovalStatus from) => Allowed[from];
}

public static class StatusChanges
{
    public static StatusEvent? Apply(Clip clip, ApprovalStatus to, string? actor, string? note, DateTime utcNow)
    {
        StatusMachine.Ensure(clip.Status, to);
        if (clip.Status == to)
        {
            return null;
        }

        var change = new StatusEvent
        {
            Id = Guid.NewGuid(),
            ClipId = clip.Id,
            FromStatus = clip.Status,
            ToStatus = to,
            Actor = string.IsNullOrWhiteSpace(actor) ? "Studio" : actor.Trim(),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            CreatedAt = utcNow
        };

        clip.Status = to;
        clip.UpdatedAt = utcNow;
        return change;
    }
}

public static class ReviewOrder
{
    public static int Rank(ApprovalStatus status) => status switch
    {
        ApprovalStatus.NeedsReview => 0,
        ApprovalStatus.Draft => 1,
        ApprovalStatus.Hold => 2,
        ApprovalStatus.Approved => 3,
        _ => 9
    };
}
