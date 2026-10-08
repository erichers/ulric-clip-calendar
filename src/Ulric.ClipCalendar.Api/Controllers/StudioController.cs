using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Contracts;
using Ulric.ClipCalendar.Api.Data;
using Ulric.ClipCalendar.Api.Domain;
using Ulric.ClipCalendar.Api.Media;
using Ulric.ClipCalendar.Api.Pdf;

namespace Ulric.ClipCalendar.Api.Controllers;

[ApiController]
public sealed class StudioController(AppDbContext db, FfmpegStatus ffmpeg, IConfiguration configuration) : ControllerBase
{
    [HttpGet("api/health")]
    public IActionResult Health() => Ok(new { status = "ok" });

    [HttpGet("api/capabilities")]
    public CapabilitiesDto Capabilities() => new(ffmpeg.IsAvailable, ffmpeg.Message);

    [HttpGet("api/review")]
    public async Task<ActionResult<IReadOnlyList<ClipDto>>> Review([FromQuery] bool includeApproved = false)
    {
        var clips = await db.Clips.Include(clip => clip.Brand).ToListAsync();
        if (!includeApproved)
        {
            clips = clips.Where(clip => clip.Status != ApprovalStatus.Approved).ToList();
        }

        return clips
            .OrderBy(clip => ReviewOrder.Rank(clip.Status))
            .ThenBy(clip => clip.PostDate)
            .ThenBy(clip => clip.PostTime)
            .ThenBy(clip => clip.Title, StringComparer.Ordinal)
            .Select(clip => ApiMapper.ToClip(clip, null, false, configuration["PublicBaseUrl"]))
            .ToList();
    }

    [HttpGet("api/stats")]
    public async Task<ActionResult<StatsDto>> Stats([FromQuery] Guid? brandId, [FromQuery] string? from, [FromQuery] string? to)
    {
        var query = db.Clips.Include(clip => clip.Brand).AsQueryable();
        if (brandId is Guid brand)
        {
            query = query.Where(clip => clip.BrandId == brand);
        }

        if (!string.IsNullOrWhiteSpace(from))
        {
            if (!ScheduleRules.TryParseDate(from, out var start))
            {
                return BadRequest(new { title = "From date should look like 2026-10-01." });
            }

            query = query.Where(clip => clip.PostDate >= start);
        }

        if (!string.IsNullOrWhiteSpace(to))
        {
            if (!ScheduleRules.TryParseDate(to, out var end))
            {
                return BadRequest(new { title = "To date should look like 2026-10-31." });
            }

            query = query.Where(clip => clip.PostDate <= end);
        }

        var clips = await query.ToListAsync();
        var byStatus = Enum.GetValues<ApprovalStatus>()
            .Select(status => new StatusCountDto(ApiMapper.Camel(status), clips.Count(clip => clip.Status == status)))
            .ToArray();
        var byBrand = clips
            .GroupBy(clip => clip.BrandId)
            .Select(group =>
            {
                var sample = group.First();
                return new BrandCountDto(sample.Brand?.Name ?? "Brand", sample.Brand?.Color ?? "#6b8f71", group.Count());
            })
            .OrderBy(row => row.Brand, StringComparer.Ordinal)
            .ToArray();
        return new StatsDto(byStatus, byBrand);
    }

    [HttpGet("api/export/csv")]
    public async Task<IActionResult> Csv([FromQuery] Guid? brandId, [FromQuery] string? from, [FromQuery] string? to)
    {
        var clips = await ExportQuery(brandId, from, to);
        if (clips.Error is not null)
        {
            return BadRequest(new { title = clips.Error });
        }

        var csv = CsvExporter.Export(CsvExporter.Rows(clips.Clips!, configuration["PublicBaseUrl"]));
        return File(Encoding.UTF8.GetBytes(csv), "text/csv", "schedule.csv");
    }

    [HttpGet("api/export/pdf")]
    public async Task<IActionResult> Pdf([FromQuery] Guid? brandId, [FromQuery] string? from, [FromQuery] string? to)
    {
        var clips = await ExportQuery(brandId, from, to);
        if (clips.Error is not null)
        {
            return BadRequest(new { title = clips.Error });
        }

        var rows = CsvExporter.Rows(clips.Clips!, configuration["PublicBaseUrl"]);
        var title = brandId is null ? "Clip schedule" : rows.FirstOrDefault()?.Brand ?? "Clip schedule";
        var bytes = SchedulePdf.Build(rows, title);
        return File(bytes, "application/pdf", "schedule.pdf");
    }

    private async Task<(string? Error, List<Clip>? Clips)> ExportQuery(Guid? brandId, string? from, string? to)
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

        var clips = await query
            .OrderBy(clip => clip.PostDate)
            .ThenBy(clip => clip.PostTime)
            .ThenBy(clip => clip.Title)
            .ToListAsync();
        return (null, clips);
    }
}
