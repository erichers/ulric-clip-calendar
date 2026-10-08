using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Contracts;
using Ulric.ClipCalendar.Api.Data;
using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Api.Controllers;

[ApiController]
[Route("api/brands")]
public sealed class BrandsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BrandDto>>> List()
    {
        var brands = await db.Brands.OrderBy(brand => brand.Name).ToListAsync();
        var counts = await db.Clips
            .GroupBy(clip => clip.BrandId)
            .Select(group => new { group.Key, Count = group.Count() })
            .ToDictionaryAsync(row => row.Key, row => row.Count);
        return brands.Select(brand => ApiMapper.ToBrand(brand, counts.GetValueOrDefault(brand.Id))).ToList();
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BrandDto>> Get(Guid id)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == id);
        if (brand is null)
        {
            return NotFound();
        }

        var count = await db.Clips.CountAsync(clip => clip.BrandId == id);
        return ApiMapper.ToBrand(brand, count);
    }

    [HttpPost]
    public async Task<ActionResult<BrandDto>> Create(BrandWrite input)
    {
        var brand = new Brand { Id = Guid.NewGuid(), CreatedAt = DateTime.UtcNow };
        var error = await Apply(brand, input, creating: true);
        if (error is not null)
        {
            return BadRequest(new { title = error, errors = new[] { error } });
        }

        db.Brands.Add(brand);
        await db.SaveChangesAsync();
        return Created($"/api/brands/{brand.Id}", ApiMapper.ToBrand(brand, 0));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BrandDto>> Update(Guid id, BrandWrite input)
    {
        var brand = await db.Brands.FirstOrDefaultAsync(item => item.Id == id);
        if (brand is null)
        {
            return NotFound();
        }

        var error = await Apply(brand, input, creating: false);
        if (error is not null)
        {
            return BadRequest(new { title = error, errors = new[] { error } });
        }

        await db.SaveChangesAsync();
        var count = await db.Clips.CountAsync(clip => clip.BrandId == id);
        return ApiMapper.ToBrand(brand, count);
    }

    private async Task<string?> Apply(Brand brand, BrandWrite input, bool creating)
    {
        var name = (input.Name ?? "").Trim();
        if (name.Length == 0)
        {
            return "A brand name is required.";
        }

        if (name.Length > 80)
        {
            return "Brand name must be 80 characters or fewer.";
        }

        var color = string.IsNullOrWhiteSpace(input.Color) ? "#6b8f71" : input.Color.Trim();
        if (color.Length != 7 || color[0] != '#' || !color[1..].All(Uri.IsHexDigit))
        {
            return "Color should look like #3f6b4e.";
        }

        if (!TryCadence(input.CadenceDays, out var cadenceDays))
        {
            return "Cadence days use 0 to 6, separated by commas. 0 is Sunday.";
        }

        var timeText = string.IsNullOrWhiteSpace(input.DefaultPostTime)
            ? (creating ? "11:00" : brand.DefaultPostTime.ToString("HH:mm"))
            : input.DefaultPostTime.Trim();
        if (!ScheduleRules.TryParseTime(timeText, out var time))
        {
            return "Default post time should look like 11:00.";
        }

        var latitude = input.Latitude ?? (creating ? 0 : brand.Latitude);
        var longitude = input.Longitude ?? (creating ? 0 : brand.Longitude);
        if (latitude is < -90 or > 90 || longitude is < -180 or > 180)
        {
            return "Latitude must be between -90 and 90, and longitude between -180 and 180.";
        }

        brand.Name = name;
        brand.Slug = await UniqueSlug(name, creating ? null : brand.Id);
        brand.Color = color.ToLowerInvariant();
        brand.Description = (input.Description ?? "").Trim();
        brand.Instagram = (input.Instagram ?? "").Trim();
        brand.TikTok = (input.TikTok ?? "").Trim();
        brand.YouTube = (input.YouTube ?? "").Trim();
        brand.Facebook = (input.Facebook ?? "").Trim();
        brand.DefaultHashtags = (input.DefaultHashtags ?? "").Trim();
        brand.CadenceLabel = (input.CadenceLabel ?? "").Trim();
        brand.CadenceDays = cadenceDays;
        brand.DefaultPostTime = time;
        brand.Latitude = latitude;
        brand.Longitude = longitude;
        brand.LocationLabel = (input.LocationLabel ?? "").Trim();
        return null;
    }

    private async Task<string> UniqueSlug(string name, Guid? self)
    {
        var slug = Slugs.Make(name);
        var baseSlug = slug;
        var n = 2;
        while (await db.Brands.AnyAsync(brand => brand.Slug == slug && brand.Id != self))
        {
            slug = $"{baseSlug}-{n++}";
        }

        return slug;
    }

    private static bool TryCadence(string? text, out string normalized)
    {
        normalized = "";
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var days = new List<string>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, out var day) || day is < 0 or > 6)
            {
                return false;
            }

            days.Add(day.ToString());
        }

        normalized = string.Join(',', days.Distinct());
        return true;
    }
}
