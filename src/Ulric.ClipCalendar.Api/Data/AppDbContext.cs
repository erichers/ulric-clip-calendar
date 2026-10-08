using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions options) : base(options)
    {
    }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Clip> Clips => Set<Clip>();
    public DbSet<ClipComment> Comments => Set<ClipComment>();
    public DbSet<StatusEvent> StatusEvents => Set<StatusEvent>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        if (Database.IsMySql())
        {
            model.HasCharSet("utf8mb4");
            model.UseCollation("utf8mb4_general_ci");
        }

        model.Entity<Brand>().Property(brand => brand.Name).HasMaxLength(160);
        model.Entity<Brand>().Property(brand => brand.Slug).HasMaxLength(80);
        model.Entity<Brand>().Property(brand => brand.Color).HasMaxLength(16);
        model.Entity<Brand>().Property(brand => brand.Description).HasMaxLength(1000);
        model.Entity<Brand>().Property(brand => brand.Instagram).HasMaxLength(80);
        model.Entity<Brand>().Property(brand => brand.TikTok).HasMaxLength(80);
        model.Entity<Brand>().Property(brand => brand.YouTube).HasMaxLength(80);
        model.Entity<Brand>().Property(brand => brand.Facebook).HasMaxLength(80);
        model.Entity<Brand>().Property(brand => brand.DefaultHashtags).HasMaxLength(400);
        model.Entity<Brand>().Property(brand => brand.CadenceLabel).HasMaxLength(80);
        model.Entity<Brand>().Property(brand => brand.CadenceDays).HasMaxLength(40);
        model.Entity<Brand>().Property(brand => brand.LocationLabel).HasMaxLength(160);
        model.Entity<Brand>().HasIndex(brand => brand.Slug).IsUnique();

        model.Entity<Clip>().Property(clip => clip.Title).HasMaxLength(200);
        model.Entity<Clip>().Property(clip => clip.Caption).HasMaxLength(4000);
        model.Entity<Clip>().Property(clip => clip.Hashtags).HasMaxLength(400);
        model.Entity<Clip>().Property(clip => clip.PlatformsCsv).HasMaxLength(200);
        model.Entity<Clip>().Property(clip => clip.Series).HasMaxLength(120);
        model.Entity<Clip>().Property(clip => clip.SourceLink).HasMaxLength(500);
        model.Entity<Clip>().Property(clip => clip.OriginalFileName).HasMaxLength(260);
        model.Entity<Clip>().Property(clip => clip.OriginalPath).HasMaxLength(300);
        model.Entity<Clip>().Property(clip => clip.ProcessedPath).HasMaxLength(300);
        model.Entity<Clip>().Property(clip => clip.ThumbnailPath).HasMaxLength(300);
        model.Entity<Clip>().Property(clip => clip.VideoCodec).HasMaxLength(40);
        model.Entity<Clip>().Property(clip => clip.MediaMessage).HasMaxLength(500);
        model.Entity<Clip>().HasIndex(clip => new { clip.BrandId, clip.PostDate });

        model.Entity<ClipComment>().Property(comment => comment.Author).HasMaxLength(120);
        model.Entity<ClipComment>().Property(comment => comment.Body).HasMaxLength(4000);

        model.Entity<StatusEvent>().Property(change => change.Actor).HasMaxLength(120);
        model.Entity<StatusEvent>().Property(change => change.Note).HasMaxLength(2000);

        model.Entity<ShareLink>().Property(link => link.Token).HasMaxLength(80);
        model.Entity<ShareLink>().Property(link => link.Label).HasMaxLength(160);
        model.Entity<ShareLink>().HasIndex(link => link.Token).IsUnique();

        model.Entity<Clip>()
            .HasOne(clip => clip.Brand)
            .WithMany(brand => brand.Clips)
            .HasForeignKey(clip => clip.BrandId)
            .OnDelete(DeleteBehavior.Cascade);

        model.Entity<ClipComment>()
            .HasOne(comment => comment.Clip)
            .WithMany(clip => clip.Comments)
            .HasForeignKey(comment => comment.ClipId)
            .OnDelete(DeleteBehavior.Cascade);

        model.Entity<StatusEvent>()
            .HasOne(change => change.Clip)
            .WithMany(clip => clip.History)
            .HasForeignKey(change => change.ClipId)
            .OnDelete(DeleteBehavior.Cascade);

        model.Entity<ShareLink>()
            .HasOne(link => link.Brand)
            .WithMany()
            .HasForeignKey(link => link.BrandId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}

public sealed class SqliteAppDbContext : AppDbContext
{
    public SqliteAppDbContext(DbContextOptions<SqliteAppDbContext> options) : base(options)
    {
    }
}

public sealed class MySqlAppDbContext : AppDbContext
{
    public MySqlAppDbContext(DbContextOptions<MySqlAppDbContext> options) : base(options)
    {
    }
}
