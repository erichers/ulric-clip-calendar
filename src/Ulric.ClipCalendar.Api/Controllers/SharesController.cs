using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Contracts;
using Ulric.ClipCalendar.Api.Data;
using Ulric.ClipCalendar.Api.Domain;
using Ulric.ClipCalendar.Api.Media;

namespace Ulric.ClipCalendar.Api.Controllers;

[ApiController]
[Route("api/shares")]
public sealed class SharesController(AppDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ShareDto>>> List()
    {
        var links = await db.ShareLinks.Include(link => link.Brand).OrderByDescending(link => link.CreatedAt).ToListAsync();
        return links.Select(link => ApiMapper.ToShare(link, configuration["PublicBaseUrl"])).ToList();
    }

    [HttpPost]
    public async Task<ActionResult<ShareDto>> Create(ShareWrite input)
    {
        DateOnly? start = null;
        DateOnly? end = null;
        if (!string.IsNullOrWhiteSpace(input.RangeStart))
        {
            if (!ScheduleRules.TryParseDate(input.RangeStart, out var parsed))
            {
                return BadRequest(new { title = "Range start should look like 2026-10-01." });
            }

            start = parsed;
        }

        if (!string.IsNullOrWhiteSpace(input.RangeEnd))
        {
            if (!ScheduleRules.TryParseDate(input.RangeEnd, out var parsed))
            {
                return BadRequest(new { title = "Range end should look like 2026-10-31." });
            }

            end = parsed;
        }

        if (start is DateOnly rangeStart && end is DateOnly rangeEnd && rangeEnd < rangeStart)
        {
            return BadRequest(new { title = "Range end is before the start." });
        }

        if (input.BrandId is null && start is null && end is null)
        {
            return BadRequest(new { title = "Choose a brand, a date range, or both." });
        }

        Brand? brand = null;
        if (input.BrandId is Guid brandId)
        {
            brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == brandId);
            if (brand is null)
            {
                return BadRequest(new { title = "That brand was not found." });
            }
        }

        var label = (input.Label ?? "").Trim();
        if (label.Length == 0)
        {
            label = brand is null ? "Shared schedule" : brand.Name + " calendar";
        }

        if (label.Length > 120)
        {
            return BadRequest(new { title = "Label must be 120 characters or fewer." });
        }

        var link = new ShareLink
        {
            Id = Guid.NewGuid(),
            Token = ShareTokens.Create(),
            BrandId = brand?.Id,
            Brand = brand,
            RangeStart = start,
            RangeEnd = end,
            Label = label,
            CreatedAt = DateTime.UtcNow
        };
        db.ShareLinks.Add(link);
        await db.SaveChangesAsync();
        return Created(PublicUrls.Combine(configuration["PublicBaseUrl"], $"s/{link.Token}"), ApiMapper.ToShare(link, configuration["PublicBaseUrl"]));
    }
}

[ApiController]
[Route("api/public")]
public sealed class PublicController(AppDbContext db, StorageLayout storage, IConfiguration configuration) : ControllerBase
{
    [HttpGet("{token}")]
    public async Task<ActionResult<PublicScheduleDto>> Get(string token)
    {
        var link = await db.ShareLinks.Include(item => item.Brand).FirstOrDefaultAsync(item => item.Token == token);
        if (link is null)
        {
            return NotFound();
        }

        var clips = await db.Clips.Include(clip => clip.Brand).ToListAsync();
        var visible = ShareScope.Visible(link, clips);
        return new PublicScheduleDto(
            link.Label,
            link.Brand?.Name,
            link.Brand?.Color,
            link.RangeStart?.ToString("yyyy-MM-dd"),
            link.RangeEnd?.ToString("yyyy-MM-dd"),
            visible.Select(clip => ApiMapper.ToClip(clip, token, false, configuration["PublicBaseUrl"])).ToArray());
    }

    [HttpGet("{token}/media/{clipId:guid}")]
    public async Task<IActionResult> Media(string token, Guid clipId, [FromQuery] string kind = "preview")
    {
        var link = await db.ShareLinks.FirstOrDefaultAsync(item => item.Token == token);
        if (link is null)
        {
            return NotFound();
        }

        var clip = await db.Clips.FirstOrDefaultAsync(item => item.Id == clipId);
        if (clip is null || !ShareScope.Visible(link, new[] { clip }).Any())
        {
            return NotFound();
        }

        var relative = kind == "thumbnail" ? clip.ThumbnailPath : clip.ProcessedPath ?? clip.OriginalPath;
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
}
