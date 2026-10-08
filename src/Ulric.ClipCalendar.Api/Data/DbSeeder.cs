using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Domain;
using Ulric.ClipCalendar.Api.Media;

namespace Ulric.ClipCalendar.Api.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(
        AppDbContext db,
        MediaProcessor processor,
        StorageLayout storage,
        FfmpegStatus ffmpeg,
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
            38.4074,
            -122.9720,
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
            45.5051,
            -122.6490,
            "Fictional pin, east side",
            now);

        db.Brands.AddRange(fern, night);

        var clips = new List<Clip>
        {
            Clip(fern, today, -5, new TimeOnly(11, 0), "The flush after rain", "Rain on the tin roof, then the first flush. We waited.", "#fernandfield #forage", "InstagramReels,TikTok", "Spore Notes", 1, false, ApprovalStatus.Approved, null, "testsrc2", 0.2, 1.6),
            Clip(fern, today, -1, new TimeOnly(11, 0), "Low and slow", "Low heat, open window, slices of lion's mane on the rack.", "#fernandfield #lionsmane", "InstagramReels,YouTubeShorts", "Spore Notes", 2, false, ApprovalStatus.NeedsReview, null, "smptebars", null, null),
            Clip(fern, today, 1, new TimeOnly(11, 0), "A lion's mane morning", "A morning cup. Lion's mane, hot water, nothing clever.", "#fernandfield #morning", "TikTok,InstagramReels", "Spore Notes", 3, false, ApprovalStatus.Draft, null, "rgbtestsrc", null, null),
            Clip(fern, today, 3, new TimeOnly(18, 30), "Broth for late email", "Broth for the hour when email should have ended.", "#slowbroth #fernandfield", "InstagramReels,Facebook", "Weeknight Broth", 1, true, ApprovalStatus.Approved, null, "pal75bars", null, null),
            Clip(fern, today, 6, new TimeOnly(18, 30), "Salt, thyme, patience", "Salt, thyme, and a long simmer. The sleep line stays out until we can source it.", "#slowbroth", "YouTubeShorts,TikTok", "Weeknight Broth", 2, false, ApprovalStatus.Hold, null, "testsrc", null, null),
            Clip(fern, today, 8, new TimeOnly(11, 0), "What we leave behind", "What we leave in the woods matters as much as what we take.", "#forage #fernandfield", "InstagramReels,TikTok,YouTubeShorts", "Forage Walk", 1, false, ApprovalStatus.NeedsReview, null, "smptehdbars", null, null),
            Clip(fern, today, 10, new TimeOnly(16, 0), "Tin on the windowsill", "A tin on the windowsill, and the light that hits it at four.", "#fernandfield", "InstagramReels,Facebook", "", null, true, ApprovalStatus.Approved, "https://videos.example.com/fern/windowsill", null, null, null),
            Clip(fern, today, 13, new TimeOnly(11, 0), "The rinse and the walk", "Part four is the rinse and the walk home.", "#fernandfield #sporenotes", "TikTok", "Spore Notes", 4, false, ApprovalStatus.Draft, "https://videos.example.com/fern/rinse", null, null, null),
            Clip(night, today, -4, new TimeOnly(6, 30), "Lights on", "5:40. Lights on, grinder on, street still blue.", "#nightshiftcoffee #opening", "InstagramReels,TikTok", "Open the Window", 1, false, ApprovalStatus.Approved, null, "testsrc2", null, null),
            Clip(night, today, -2, new TimeOnly(6, 30), "The first tray", "The first tray. We taste it before we sell it.", "#nightshiftcoffee", "YouTubeShorts,InstagramReels", "Open the Window", 2, false, ApprovalStatus.NeedsReview, null, "smptebars", null, null),
            Clip(night, today, 0, new TimeOnly(6, 30), "Oat milk, honestly", "Oat milk, honestly: we steam it, we do not pretend it is dairy.", "#nightshiftcoffee #oat", "TikTok,InstagramReels", "Open the Window", 3, false, ApprovalStatus.Draft, null, "rgbtestsrc", null, null),
            Clip(night, today, 2, new TimeOnly(21, 30), "One more, then we close", "One more espresso, then the chairs go up.", "#afterhours #nightshiftcoffee", "InstagramReels,Facebook", "Last Call Espresso", 1, true, ApprovalStatus.Approved, null, "pal75bars", null, null),
            Clip(night, today, 4, new TimeOnly(21, 45), "Cups in the rack", "Cups in the rack, playlist down, door locked.", "#afterhours", "TikTok,YouTubeShorts", "Last Call Espresso", 2, false, ApprovalStatus.Hold, null, "testsrc", null, null),
            Clip(night, today, 7, new TimeOnly(15, 0), "Playlist for the pour", "A pour-over playlist for the slow hour.", "#coffeewindow #nightshiftcoffee", "YouTubeShorts,TikTok", "Shift Notes", 1, false, ApprovalStatus.NeedsReview, null, "smptehdbars", null, null),
            Clip(night, today, 9, new TimeOnly(9, 0), "Saturday regulars", "Saturday regulars, same corner, same order.", "#nightshiftcoffee", "InstagramReels,Facebook", "", null, true, ApprovalStatus.Approved, "https://videos.example.com/nightshift/regulars", null, null, null),
            Clip(night, today, 12, new TimeOnly(6, 30), "Same bell, new beans", "The window opens again. Same bell, new beans.", "#nightshiftcoffee #opening", "TikTok,InstagramReels", "Open the Window", 4, false, ApprovalStatus.Draft, "https://videos.example.com/nightshift/bell", null, null, null)
        };

        db.Clips.AddRange(clips);
        AddThread(clips, "Low and slow", now,
            new[]
            {
                ("Jules Okonkwo", "The drying shot is clear. Can the last line stay sensory, with no health claim?", -2),
                ("Avery Chen", "Rewrote it. Ready for another look.", -1)
            },
            new[] { (ApprovalStatus.Draft, ApprovalStatus.NeedsReview, "Avery Chen", "Ready for a look.", -3) });
        AddThread(clips, "Salt, thyme, patience", now,
            new[] { ("Jules Okonkwo", "Holding this one. The sleep line needs a source or a cut.", -1) },
            new[]
            {
                (ApprovalStatus.Draft, ApprovalStatus.NeedsReview, "Avery Chen", "Cut is in.", -4),
                (ApprovalStatus.NeedsReview, ApprovalStatus.Hold, "Jules Okonkwo", "Hold the sleep line until we have a source.", -1)
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

        await db.SaveChangesAsync(ct);

        var fileClips = clips.Where(clip => clip.OriginalPath is not null).ToList();
        if (!ffmpeg.IsAvailable)
        {
            foreach (var clip in fileClips)
            {
                clip.MediaState = MediaJobState.Unavailable;
                clip.MediaMessage = ffmpeg.Message;
            }

            await db.SaveChangesAsync(ct);
            logger.LogWarning("Seeded demo clips without video files because ffmpeg is missing.");
            return;
        }

        await Parallel.ForEachAsync(fileClips, new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct }, async (clip, token) =>
        {
            var full = storage.Resolve(clip.OriginalPath);
            if (full is null)
            {
                return;
            }

            var pattern = clip.VideoCodec ?? "testsrc2";
            var lavfi = $"{pattern}=size=360x640:rate=12:duration=2";
            try
            {
                await FfmpegRunner.WriteDemoAsync(ffmpeg.FfmpegPath, full, lavfi, token);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Demo video for {Title} was not generated.", clip.Title);
            }
        });

        foreach (var clip in fileClips)
        {
            clip.VideoCodec = null;
            await processor.ProcessAsync(clip.Id, ct);
        }

        logger.LogInformation("Seeded {Count} demo clips.", clips.Count);
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

    private static Clip Clip(
        Brand brand,
        DateOnly today,
        int offset,
        TimeOnly time,
        string title,
        string caption,
        string hashtags,
        string platforms,
        string series,
        int? part,
        bool stories,
        ApprovalStatus status,
        string? link,
        string? pattern,
        double? trimStart,
        double? trimEnd)
    {
        var id = Guid.NewGuid();
        var hasFile = pattern is not null;
        return new Clip
        {
            Id = id,
            BrandId = brand.Id,
            Brand = brand,
            Title = title,
            Caption = caption,
            Hashtags = hashtags,
            PlatformsCsv = platforms,
            Series = series,
            SeriesPart = part,
            PostDate = today.AddDays(offset),
            PostTime = time,
            StoriesOk = stories,
            Status = status,
            SourceKind = hasFile ? ClipSourceKind.Upload : ClipSourceKind.Link,
            SourceLink = link,
            OriginalFileName = hasFile ? "demo.mp4" : null,
            OriginalPath = hasFile ? $"media/{id}/original.mp4" : null,
            TrimStartSeconds = trimStart,
            TrimEndSeconds = trimEnd,
            MediaState = hasFile ? MediaJobState.Pending : MediaJobState.Unavailable,
            MediaMessage = hasFile ? null : "Linked clips stay on their original URL. Upload a file if you want a thumbnail or a trim.",
            VideoCodec = pattern,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    private static void AddThread(
        List<Clip> clips,
        string title,
        DateTime now,
        (string Author, string Body, int HourOffset)[] comments,
        (ApprovalStatus From, ApprovalStatus To, string Actor, string Note, int HourOffset)[] history)
    {
        var clip = clips.First(item => item.Title == title);
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
}
