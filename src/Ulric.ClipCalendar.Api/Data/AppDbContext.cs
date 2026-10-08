using Microsoft.EntityFrameworkCore;
using Ulric.ClipCalendar.Api.Domain;

namespace Ulric.ClipCalendar.Api.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<Clip> Clips => Set<Clip>();
    public DbSet<ClipComment> Comments => Set<ClipComment>();
    public DbSet<StatusEvent> StatusEvents => Set<StatusEvent>();
    public DbSet<ShareLink> ShareLinks => Set<ShareLink>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.Entity<Brand>().HasIndex(brand => brand.Slug).IsUnique();
        model.Entity<Clip>().HasIndex(clip => new { clip.BrandId, clip.PostDate });
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
