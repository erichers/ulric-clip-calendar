using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Contracts;
using Ulric.ClipCalendar.Api.Data;
using Ulric.ClipCalendar.Api.Domain;
using Ulric.ClipCalendar.Api.Media;

namespace Ulric.ClipCalendar.Api.Controllers;

[ApiController]
[Route("api/clips")]
public sealed class ClipsController(AppDbContext db, StorageLayout storage, MediaQueue queue, IConfiguration configuration) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".webm", ".m4v", ".mkv"
    };

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ClipDto>>> List(
        [FromQuery] Guid? brandId,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? status)
    {
        var filtered = await Filter(brandId, from, to, status);
        if (filtered.Error is not null)
        {
            return BadRequest(new { title = filtered.Error });
        }

        return filtered.Clips!.Select(clip => ApiMapper.ToClip(clip, null, false, configuration["PublicBaseUrl"])).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClipDto>> Get(Guid id)
    {
        var clip = await Load(id);
        return clip is null ? NotFound() : ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]);
    }

    [HttpPost]
    public async Task<ActionResult<ClipDto>> Create(ClipInput input)
    {
        ClipParsed parsed;
        try
        {
            parsed = ClipRules.Parse(input);
        }
        catch (ClipValidationException ex)
        {
            return BadRequest(new { title = ex.Message, errors = ex.Errors });
        }

        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == input.BrandId);
        if (brand is null)
        {
            return BadRequest(new { title = "Choose a brand." });
        }

        var now = DateTime.UtcNow;
        var clip = new Clip
        {
            Id = Guid.NewGuid(),
            BrandId = brand.Id,
            Brand = brand,
            CreatedAt = now,
            UpdatedAt = now,
            Status = parsed.Status ?? ApprovalStatus.Draft
        };
        ApplyFields(clip, parsed, brand, creating: true);
        try
        {
            ScheduleRules.Move(clip, parsed.PostDate, ResolveTime(parsed, brand, clip, creating: true), now);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(clip.OriginalPath))
        {
            ApplyLinkOnlyState(clip, parsed.SourceLink);
        }

        db.Clips.Add(clip);
        await db.SaveChangesAsync();
        return Created(PublicUrls.Combine(configuration["PublicBaseUrl"], $"api/clips/{clip.Id}"), ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ClipDto>> Update(Guid id, ClipInput input)
    {
        var clip = await Load(id);
        if (clip is null)
        {
            return NotFound();
        }

        ClipParsed parsed;
        try
        {
            parsed = ClipRules.Parse(input);
        }
        catch (ClipValidationException ex)
        {
            return BadRequest(new { title = ex.Message, errors = ex.Errors });
        }

        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == input.BrandId);
        if (brand is null)
        {
            return BadRequest(new { title = "Choose a brand." });
        }

        if (parsed.Status is ApprovalStatus next && next != clip.Status)
        {
            try
            {
                StatusMachine.Ensure(clip.Status, next);
            }
            catch (InvalidStatusTransitionException ex)
            {
                return BadRequest(new { title = ex.Message });
            }
        }

        ApplyFields(clip, parsed, brand, creating: false);
        try
        {
            ScheduleRules.Move(clip, parsed.PostDate, ResolveTime(parsed, brand, clip, creating: false), DateTime.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(clip.OriginalPath))
        {
            ApplyLinkOnlyState(clip, parsed.SourceLink);
        }
        else
        {
            clip.SourceKind = ClipSourceKind.Upload;
        }

        if (parsed.Status is ApprovalStatus status && status != clip.Status)
        {
            var change = StatusChanges.Apply(clip, status, input.Actor, "Updated from the clip editor.", DateTime.UtcNow);
            if (change is not null)
            {
                db.StatusEvents.Add(change);
            }
        }

        await db.SaveChangesAsync();
        return ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]);
    }

    [HttpPost("{id:guid}/reschedule")]
    public async Task<ActionResult<ClipDto>> Reschedule(Guid id, RescheduleRequest input)
    {
        var clip = await Load(id);
        if (clip is null)
        {
            return NotFound();
        }

        if (!ScheduleRules.TryParseDate(input.PostDate, out var date))
        {
            return BadRequest(new { title = "Post date should look like 2026-10-08." });
        }

        TimeOnly? time = null;
        if (!string.IsNullOrWhiteSpace(input.PostTime))
        {
            if (!ScheduleRules.TryParseTime(input.PostTime, out var parsed))
            {
                return BadRequest(new { title = "Post time should look like 11:00." });
            }

            time = parsed;
        }

        try
        {
            ScheduleRules.Move(clip, date, time, DateTime.UtcNow);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { title = ex.Message });
        }

        await db.SaveChangesAsync();
        return ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]);
    }

    [HttpPost("{id:guid}/status")]
    public async Task<ActionResult<ClipDto>> Status(Guid id, StatusRequest input)
    {
        var clip = await Load(id);
        if (clip is null)
        {
            return NotFound();
        }

        try
        {
            var change = StatusChanges.Apply(clip, input.Status, input.Actor, input.Note, DateTime.UtcNow);
            if (change is not null)
            {
                db.StatusEvents.Add(change);
                await db.SaveChangesAsync();
            }
        }
        catch (InvalidStatusTransitionException ex)
        {
            return BadRequest(new { title = ex.Message });
        }

        return ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]);
    }

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<ClipDto>> Comment(Guid id, CommentRequest input)
    {
        var clip = await Load(id);
        if (clip is null)
        {
            return NotFound();
        }

        var author = (input.Author ?? "").Trim();
        var body = (input.Body ?? "").Trim();
        if (author.Length is 0 or > 80)
        {
            return BadRequest(new { title = "Add your name (80 characters or fewer)." });
        }

        if (body.Length is 0 or > 2000)
        {
            return BadRequest(new { title = "Comment must be between 1 and 2000 characters." });
        }

        db.Comments.Add(new ClipComment
        {
            Id = Guid.NewGuid(),
            ClipId = clip.Id,
            Author = author,
            Body = body,
            CreatedAt = DateTime.UtcNow
        });
        clip.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        clip = (await Load(id))!;
        return ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]);
    }

    [HttpPost("{id:guid}/file")]
    [RequestSizeLimit(80_000_000)]
    [RequestFormLimits(MultipartBodyLengthLimit = 80_000_000)]
    public async Task<ActionResult<ClipDto>> Upload(Guid id, IFormFile? file)
    {
        var clip = await Load(id);
        if (clip is null)
        {
            return NotFound();
        }

        if (file is null || file.Length == 0)
        {
            return BadRequest(new { title = "Choose a video file." });
        }

        if (file.Length > 80_000_000)
        {
            return BadRequest(new { title = "Video files must be 80 MB or smaller." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!AllowedExtensions.Contains(extension))
        {
            return BadRequest(new { title = "Use an mp4, mov, webm, m4v, or mkv file." });
        }

        DeleteStored(clip.ProcessedPath);
        DeleteStored(clip.ThumbnailPath);
        DeleteStored(clip.OriginalPath);

        var directory = storage.ClipDirectory(clip.Id);
        var absolute = Path.Combine(directory, "original" + extension.ToLowerInvariant());
        await using (var stream = System.IO.File.Create(absolute))
        {
            await file.CopyToAsync(stream);
        }

        clip.OriginalFileName = Path.GetFileName(file.FileName);
        clip.OriginalPath = storage.Relative(absolute);
        clip.ProcessedPath = null;
        clip.ThumbnailPath = null;
        clip.DurationSeconds = null;
        clip.Width = null;
        clip.Height = null;
        clip.VideoCodec = null;
        clip.SourceKind = ClipSourceKind.Upload;
        clip.MediaState = MediaJobState.Pending;
        clip.MediaMessage = "Queued for a thumbnail and preview.";
        clip.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        queue.Enqueue(clip.Id);
        return ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]);
    }

    [HttpPost("{id:guid}/trim")]
    public async Task<ActionResult<ClipDto>> Trim(Guid id, TrimRequest input)
    {
        var clip = await Load(id);
        if (clip is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(clip.OriginalPath))
        {
            return BadRequest(new { title = "Upload a video before trimming. Links are not fetched." });
        }

        if (input.Start < 0 || input.End <= input.Start)
        {
            return BadRequest(new { title = "Trim end must be after the start, and the start cannot be negative." });
        }

        if (clip.DurationSeconds is double duration && input.End > duration + 0.05)
        {
            return BadRequest(new { title = "Trim end is past the end of the file." });
        }

        clip.TrimStartSeconds = input.Start;
        clip.TrimEndSeconds = input.End;
        clip.MediaState = MediaJobState.Pending;
        clip.MediaMessage = "Trim queued. The original file stays put.";
        clip.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        queue.Enqueue(clip.Id);
        return ApiMapper.ToClip(clip, null, true, configuration["PublicBaseUrl"]);
    }

    [HttpGet("{id:guid}/media")]
    public async Task<IActionResult> Media(Guid id, [FromQuery] string kind = "preview")
    {
        var clip = await db.Clips.FirstOrDefaultAsync(item => item.Id == id);
        if (clip is null)
        {
            return NotFound();
        }

        var relative = kind switch
        {
            "thumbnail" => clip.ThumbnailPath,
            "original" => clip.OriginalPath,
            "processed" => clip.ProcessedPath,
            _ => clip.ProcessedPath ?? clip.OriginalPath
        };
        return Serve(relative);
    }

    private IActionResult Serve(string? relative)
    {
        var full = storage.Resolve(relative);
        if (full is null || !System.IO.File.Exists(full))
        {
            return NotFound();
        }

        var extension = Path.GetExtension(full).ToLowerInvariant();
        var type = extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".png" => "image/png",
            ".webm" => "video/webm",
            ".mov" => "video/quicktime",
            _ => "video/mp4"
        };
        return PhysicalFile(full, type, enableRangeProcessing: true);
    }

    private void DeleteStored(string? relative)
    {
        var full = storage.Resolve(relative);
        if (full is not null && System.IO.File.Exists(full))
        {
            System.IO.File.Delete(full);
        }
    }

    private async Task<Clip?> Load(Guid id) =>
        await db.Clips
            .Include(clip => clip.Brand)
            .Include(clip => clip.Comments)
            .Include(clip => clip.History)
            .FirstOrDefaultAsync(clip => clip.Id == id);

    private async Task<(string? Error, List<Clip>? Clips)> Filter(Guid? brandId, string? from, string? to, string? status)
    {
        DateOnly? start = null;
        DateOnly? end = null;
        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!ScheduleRules.TryParseDate(from, out var parsed))
            {
                return ("From date should look like 2026-10-01.", null);
            }

            start = parsed;
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!ScheduleRules.TryParseDate(to, out var parsed))
            {
                return ("To date should look like 2026-10-31.", null);
            }

            end = parsed;
        }

        if (start is DateOnly rangeStart && end is DateOnly rangeEnd && rangeEnd < rangeStart)
        {
            return ("Range end is before the start.", null);
        }

        ApprovalStatus? parsedStatus = null;
        if (!string.IsNullOrWhiteSpace(status))
        {
            if (!Enum.TryParse<ApprovalStatus>(status, ignoreCase: true, out var value))
            {
                return ("Unknown status.", null);
            }

            parsedStatus = value;
        }

        var query = db.Clips.Include(clip => clip.Brand).AsQueryable();
        if (brandId is Guid brand)
        {
            query = query.Where(clip => clip.BrandId == brand);
        }

        if (start is DateOnly fromDate)
        {
            query = query.Where(clip => clip.PostDate >= fromDate);
        }

        if (end is DateOnly toDate)
        {
            query = query.Where(clip => clip.PostDate <= toDate);
        }

        if (parsedStatus is ApprovalStatus approval)
        {
            query = query.Where(clip => clip.Status == approval);
        }

        var clips = await query
            .OrderBy(clip => clip.PostDate)
            .ThenBy(clip => clip.PostTime)
            .ThenBy(clip => clip.Title)
            .ToListAsync();
        return (null, clips);
    }

    private static void ApplyFields(Clip clip, ClipParsed parsed, Brand brand, bool creating)
    {
        clip.BrandId = brand.Id;
        clip.Brand = brand;
        clip.Title = parsed.Title;
        clip.Caption = parsed.Caption;
        clip.Hashtags = parsed.Hashtags;
        clip.PlatformsCsv = PlatformList.Format(parsed.Platforms);
        clip.Series = parsed.Series;
        clip.SeriesPart = parsed.SeriesPart;
        clip.StoriesOk = parsed.StoriesOk;
        clip.SourceLink = parsed.SourceLink;
        if (creating)
        {
            clip.Status = parsed.Status ?? ApprovalStatus.Draft;
        }
    }

    private static TimeOnly? ResolveTime(ClipParsed parsed, Brand brand, Clip clip, bool creating)
    {
        if (parsed.PostTimeText is null)
        {
            return creating ? brand.DefaultPostTime : clip.PostTime;
        }

        ScheduleRules.TryParseTime(parsed.PostTimeText, out var time);
        return time;
    }

    private static void ApplyLinkOnlyState(Clip clip, string? link)
    {
        if (link is null)
        {
            clip.SourceKind = ClipSourceKind.Upload;
            clip.MediaState = MediaJobState.NotRequested;
            clip.MediaMessage = "Add a video file or a link.";
            return;
        }

        clip.SourceKind = ClipSourceKind.Link;
        clip.MediaState = MediaJobState.Unavailable;
        clip.MediaMessage = "Linked clips stay on their original URL. Upload a file if you want a thumbnail or a trim.";
    }
}
