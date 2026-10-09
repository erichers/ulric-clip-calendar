using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Domain;
using Ulric.ClipCalendar.Api.Media;

namespace Ulric.ClipCalendar.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        StorageLayout storage,
        string contentRoot,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (await db.Brands.AnyAsync(ct))
        {
            logger.LogInformation("Seed skipped because brands already exist.");
            return;
        }

        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var fern = Brand(
            "Fern & Field",
            "#3f6b4e",
            "A small mushroom wellness brand. Broths, tinctures, and notes from the woods.",
            "fernandfield",
            "fernandfield",
            "@fernandfield",
            "fernandfield",
            "#fernandfield #mushroomwellness #slowbroth",
            "Tue, Thu, Sat",
            "2,4,6",
            new TimeOnly(11, 0),
            38.41,
            -122.97,
            "Fictional pin, west county",
            now);
        var night = Brand(
            "Night Shift Coffee",
            "#8c4a32",
            "A late cafe. Espresso before dawn and one last cup after midnight.",
            "nightshiftcoffee",
            "nightshiftcoffee",
            "@nightshiftcoffee",
            "nightshiftcoffee",
            "#nightshiftcoffee #coffeewindow #afterhours",
            "Mon, Wed, Fri, Sun",
            "1,3,5,0",
            new TimeOnly(6, 30),
            45.51,
            -122.65,
            "Fictional pin, east side",
            now);

        db.Brands.AddRange(fern, night);

        var seedMedia = LocateSeedMedia(contentRoot);
        if (seedMedia is null)
        {
            logger.LogWarning("Seed media folder was not found. Clips will be saved without files.");
        }

        var clips = new List<Clip>();
        clips.AddRange(Schedule(fern, FernPieces, today, now));
        clips.AddRange(Schedule(night, NightPieces, today, now));
        db.Clips.AddRange(clips);

        if (!clips.Any(clip => clip.Status == ApprovalStatus.Hold))
        {
            var holdTarget = clips.First(clip => clip.PostDate > today);
            holdTarget.Status = ApprovalStatus.Hold;
        }

        var fernReview = clips.First(clip => clip.BrandId == fern.Id && clip.Status == ApprovalStatus.NeedsReview);
        var nightReview = clips.First(clip => clip.BrandId == night.Id && clip.Status == ApprovalStatus.NeedsReview);
        var held = clips.First(clip => clip.Status == ApprovalStatus.Hold);
        var approved = clips.First(clip => clip.Status == ApprovalStatus.Approved);

        AddThread(fernReview, now,
            new[]
            {
                ("Jules Okonkwo", "The shot is clear. Can the last line stay sensory, with no health claim?", -6),
                ("Avery Chen", "Rewrote it. Ready for another look.", -2)
            },
            new[] { (ApprovalStatus.Draft, ApprovalStatus.NeedsReview, "Avery Chen", "Ready for a look.", -2) });
        AddThread(nightReview, now,
            new[] { ("Jules Okonkwo", "Love the window light. Trim the last second if the pour runs long.", -3) },
            new[] { (ApprovalStatus.Draft, ApprovalStatus.NeedsReview, "Avery Chen", "In the queue for today.", -3) });
        AddThread(held, now,
            new[] { ("Jules Okonkwo", "Holding this one. The sleep line needs a source or a cut.", -8) },
            new[]
            {
                (ApprovalStatus.Draft, ApprovalStatus.NeedsReview, "Avery Chen", "Cut is in.", -20),
                (ApprovalStatus.NeedsReview, ApprovalStatus.Hold, "Jules Okonkwo", "Hold until we have a source for that line.", -8)
            });
        AddThread(approved, now,
            Array.Empty<(string, string, int)>(),
            new[]
            {
                (ApprovalStatus.Draft, ApprovalStatus.NeedsReview, "Avery Chen", "Posted to the queue.", -72),
                (ApprovalStatus.NeedsReview, ApprovalStatus.Approved, "Jules Okonkwo", "Approved for the calendar.", -48)
            });

        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        db.ShareLinks.AddRange(
            new ShareLink
            {
                Id = Guid.NewGuid(),
                Token = ShareTokens.Create(),
                BrandId = fern.Id,
                Label = "Fern & Field calendar",
                CreatedAt = now
            },
            new ShareLink
            {
                Id = Guid.NewGuid(),
                Token = ShareTokens.Create(),
                RangeStart = monthStart,
                RangeEnd = monthEnd,
                Label = "This month, all brands",
                CreatedAt = now
            });

        await CopyMediaAsync(clips, seedMedia, storage, logger, ct);
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Seeded {Count} clips across three months.", clips.Count);
    }

    public static string? LocateSeedMedia(string contentRoot)
    {
        var starts = new[] { contentRoot, AppContext.BaseDirectory, Directory.GetCurrentDirectory() };
        foreach (var start in starts)
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            for (var depth = 0; depth < 6 && dir is not null; depth++, dir = dir.Parent)
            {
                var candidate = Path.Combine(dir.FullName, "seed-media");
                if (File.Exists(Path.Combine(candidate, "night-latte.mp4")))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static List<Clip> Schedule(Brand brand, IReadOnlyList<Piece> pieces, DateOnly today, DateTime now)
    {
        var days = brand.CadenceDays.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToHashSet();
        var start = new DateOnly(today.Year, today.Month, 1).AddMonths(-1);
        var end = new DateOnly(today.Year, today.Month, 1).AddMonths(2).AddDays(-1);
        var parts = new Dictionary<string, int>(StringComparer.Ordinal);
        var clips = new List<Clip>();
        var index = 0;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (!days.Contains((int)date.DayOfWeek))
            {
                continue;
            }

            var piece = pieces[index % pieces.Count];
            var cycle = index / pieces.Count;
            var title = cycle == 0 ? piece.Title : $"{piece.Title}, {date:MMM d}";
            parts.TryGetValue(piece.Series, out var part);
            part += 1;
            parts[piece.Series] = part;
            var time = piece.Evening ? new TimeOnly(18, 30) : brand.DefaultPostTime;
            if (piece.Evening && brand.Name.StartsWith("Night", StringComparison.Ordinal))
            {
                time = new TimeOnly(21, 30);
            }

            clips.Add(new Clip
            {
                Id = Guid.NewGuid(),
                BrandId = brand.Id,
                Brand = brand,
                Title = title,
                Caption = piece.Caption,
                Hashtags = piece.Tags,
                PlatformsCsv = piece.Platforms,
                Series = piece.Series,
                SeriesPart = part,
                PostDate = date,
                PostTime = time,
                StoriesOk = piece.Stories,
                Status = StatusFor(date, today, index),
                SourceKind = ClipSourceKind.Upload,
                OriginalFileName = piece.File + ".mp4",
                MediaState = MediaJobState.Pending,
                CreatedAt = now,
                UpdatedAt = now,
                VideoCodec = "h264"
            });
            var clip = clips[^1];
            clip.Width = 720;
            clip.Height = 1280;
            clip.DurationSeconds = piece.File.Contains("still", StringComparison.Ordinal) ? 3 : 4;
            index++;
        }

        return clips;
    }

    private static ApprovalStatus StatusFor(DateOnly date, DateOnly today, int index)
    {
        var delta = date.DayNumber - today.DayNumber;
        if (delta < 0)
        {
            return ApprovalStatus.Approved;
        }

        if (delta == 0)
        {
            return ApprovalStatus.NeedsReview;
        }

        if (delta == 3)
        {
            return ApprovalStatus.Hold;
        }

        if (delta <= 8)
        {
            return ApprovalStatus.NeedsReview;
        }

        return index % 11 == 0 ? ApprovalStatus.NeedsReview : ApprovalStatus.Draft;
    }

    private static async Task CopyMediaAsync(
        List<Clip> clips,
        string? seedMedia,
        StorageLayout storage,
        ILogger logger,
        CancellationToken ct)
    {
        foreach (var clip in clips)
        {
            var file = Path.GetFileNameWithoutExtension(clip.OriginalFileName);
            if (seedMedia is null || string.IsNullOrWhiteSpace(file))
            {
                clip.MediaState = MediaJobState.Failed;
                clip.MediaMessage = "Seed media file was not found.";
                continue;
            }

            var video = Path.Combine(seedMedia, file + ".mp4");
            var poster = Path.Combine(seedMedia, file + ".jpg");
            if (!File.Exists(video) || !File.Exists(poster))
            {
                clip.MediaState = MediaJobState.Failed;
                clip.MediaMessage = "Seed media file was not found.";
                logger.LogWarning("Missing seed file for {File}", file);
                continue;
            }

            var folder = storage.Resolve($"media/{clip.Id}")
                ?? throw new InvalidOperationException("Storage root is not configured.");
            Directory.CreateDirectory(folder);
            var original = Path.Combine(folder, "original.mp4");
            var preview = Path.Combine(folder, "preview.mp4");
            var thumb = Path.Combine(folder, "thumb.jpg");
            await using (var source = File.OpenRead(video))
            await using (var first = File.Create(original))
            await using (var second = File.Create(preview))
            {
                await source.CopyToAsync(first, ct);
                source.Position = 0;
                await source.CopyToAsync(second, ct);
            }

            // Prefer the 4:5 thumbnail cut from the picture area (no blurred letterbox bands).
            var cropped = Path.Combine(seedMedia, "thumbs", file + ".jpg");
            File.Copy(File.Exists(cropped) ? cropped : poster, thumb, overwrite: true);
            clip.OriginalPath = storage.Relative(original);
            clip.ProcessedPath = storage.Relative(preview);
            clip.ThumbnailPath = storage.Relative(thumb);
            clip.MediaState = MediaJobState.Succeeded;
            clip.MediaMessage = "Preview is ready.";
        }
    }

    private static Brand Brand(
        string name,
        string color,
        string description,
        string instagram,
        string tiktok,
        string youtube,
        string facebook,
        string hashtags,
        string cadence,
        string days,
        TimeOnly time,
        double lat,
        double lng,
        string location,
        DateTime now) => new()
    {
        Id = Guid.NewGuid(),
        Name = name,
        Slug = Slugs.Make(name),
        Color = color,
        Description = description,
        Instagram = instagram,
        TikTok = tiktok,
        YouTube = youtube,
        Facebook = facebook,
        DefaultHashtags = hashtags,
        CadenceLabel = cadence,
        CadenceDays = days,
        DefaultPostTime = time,
        Latitude = lat,
        Longitude = lng,
        LocationLabel = location,
        CreatedAt = now
    };

    private static void AddThread(
        Clip clip,
        DateTime now,
        (string Author, string Body, int HourOffset)[] comments,
        (ApprovalStatus From, ApprovalStatus To, string Actor, string Note, int HourOffset)[] history)
    {
        foreach (var comment in comments)
        {
            clip.Comments.Add(new ClipComment
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                Author = comment.Author,
                Body = comment.Body,
                CreatedAt = now.AddHours(comment.HourOffset)
            });
        }

        foreach (var change in history)
        {
            clip.History.Add(new StatusEvent
            {
                Id = Guid.NewGuid(),
                ClipId = clip.Id,
                FromStatus = change.From,
                ToStatus = change.To,
                Actor = change.Actor,
                Note = change.Note,
                CreatedAt = now.AddHours(change.HourOffset)
            });
        }
    }

    private sealed record Piece(
        string File,
        string Title,
        string Caption,
        string Tags,
        string Series,
        string Platforms,
        bool Stories,
        bool Evening);

    private static readonly Piece[] FernPieces =
    [
        new("fern-mushroom-sunset", "The flush after rain", "Rain on the tin roof, then the first flush. We waited.", "#fernandfield #forage #sporenotes", "Spore Notes", "InstagramReels,TikTok", false, false),
        new("fern-forest-floor", "What we leave behind", "What we leave in the woods matters as much as what we take.", "#forage #fernandfield", "Forage Walk", "InstagramReels,YouTubeShorts", false, false),
        new("fern-path", "The path holds the rain", "We walk the edge of the path and leave the middle alone.", "#fernandfield #woods", "Forage Walk", "TikTok,InstagramReels", false, false),
        new("fern-canopy", "Light in pieces", "Light comes through the canopy in pieces. We work in the pieces.", "#fernandfield #woods", "Spore Notes", "YouTubeShorts,TikTok", false, false),
        new("fern-tea", "Tea for the simmer", "A glass of tea while the pot decides.", "#slowbroth #fernandfield", "Weeknight Broth", "InstagramReels,Facebook", true, true),
        new("fern-vegetables", "The board", "A sharp knife, a wooden board, and no rush.", "#fernandfield #slowbroth", "Weeknight Broth", "TikTok,YouTubeShorts", false, true),
        new("fern-fire", "Fire for the long pot", "The outdoor fire is for the long pot, not for show.", "#slowbroth #fernandfield", "Weeknight Broth", "InstagramReels,Facebook", true, true),
        new("fern-fungus", "Notes, not a basket", "Fungus on the log stays where it is. We take notes.", "#forage #fernandfield", "Forage Walk", "TikTok,InstagramReels", false, false),
        new("fern-still-mushrooms", "A basket half empty", "Half empty on purpose. The woods keep the rest.", "#fernandfield #forage", "Spore Notes", "InstagramReels,TikTok", true, false),
        new("fern-still-forest", "After the rain", "The trees are still dripping. We wait at the edge.", "#woods #fernandfield", "Forage Walk", "YouTubeShorts,InstagramReels", true, false),
        new("fern-still-fungi", "On the bark", "A close look, then we put the lens away.", "#forage #sporenotes", "Spore Notes", "TikTok", true, false),
        new("fern-still-woods", "The long way home", "The walk home is part of the recipe.", "#fernandfield #woods", "Forage Walk", "InstagramReels,Facebook", true, false),
        new("fern-still-moss", "Moss underfoot", "Stay on the moss, not on the flush.", "#forage #fernandfield", "Forage Walk", "TikTok,YouTubeShorts", true, false),
        new("fern-still-broth", "Quiet surface", "An hour in. The surface goes quiet. That is the sign.", "#slowbroth #fernandfield", "Weeknight Broth", "InstagramReels,Facebook", true, true),
        new("fern-still-herbs", "Thyme by the door", "Thyme from the pot by the door. Salt. Then we wait.", "#slowbroth #fernandfield", "Weeknight Broth", "TikTok,InstagramReels", true, true)
    ];

    private static readonly Piece[] NightPieces =
    [
        new("night-latte", "The heart is optional", "The heart in the cup is optional. The shot is not.", "#nightshiftcoffee #latte", "Open the Window", "InstagramReels,TikTok", false, false),
        new("night-steam", "The mug fogs the glass", "5:40. Lights on, grinder on, street still blue.", "#nightshiftcoffee #opening", "Open the Window", "YouTubeShorts,InstagramReels", false, false),
        new("night-beans", "New bag, same grinder", "We taste the first tray before we sell it.", "#nightshiftcoffee #beans", "Open the Window", "TikTok,YouTubeShorts", false, false),
        new("night-barista", "Milk, ice, a short line", "Oat milk, honestly: we steam it, we do not pretend it is dairy.", "#nightshiftcoffee #oat", "Shift Notes", "InstagramReels,TikTok", false, false),
        new("night-cafe", "Before the street wakes", "Someone is already at the window. Same corner, same order.", "#coffeewindow #nightshiftcoffee", "Shift Notes", "InstagramReels,Facebook", true, false),
        new("night-cappuccino", "Bitter underneath", "Sparkle on top. Bitter underneath. That is the deal.", "#nightshiftcoffee #latte", "Open the Window", "TikTok,InstagramReels", false, false),
        new("night-pour", "A steady pour", "A steady pour, then we stop. The playlist stays low.", "#coffeewindow #pour", "Shift Notes", "YouTubeShorts,TikTok", false, false),
        new("night-still-latte", "Saturday regulars", "Saturday regulars, same cup, no speech required.", "#nightshiftcoffee", "Shift Notes", "InstagramReels,Facebook", true, false),
        new("night-still-espresso", "One more, then we close", "One more espresso, then the chairs go up.", "#afterhours #nightshiftcoffee", "Last Call Espresso", "InstagramReels,TikTok", true, true),
        new("night-still-beans", "The bag we just opened", "New beans, old ritual. We write the date on the bag.", "#nightshiftcoffee #beans", "Open the Window", "YouTubeShorts,InstagramReels", true, false),
        new("night-still-cafe", "Cups in the rack", "Cups in the rack, playlist down, door locked.", "#afterhours #nightshiftcoffee", "Last Call Espresso", "TikTok,Facebook", true, true),
        new("night-still-cup", "The cup by the printer", "The cup we reach for when the ticket printer starts.", "#nightshiftcoffee #opening", "Open the Window", "InstagramReels,TikTok", true, false),
        new("night-still-chemex", "Three quiet minutes", "Grounds, a filter, and three quiet minutes.", "#pour #coffeewindow", "Shift Notes", "YouTubeShorts,TikTok", true, false)
    ];
}
