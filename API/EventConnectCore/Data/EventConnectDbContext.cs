using EventConnectCore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventConnectCore.Data;

public class EventConnectDbContext(DbContextOptions<EventConnectDbContext> options)
    : IdentityDbContext<User, IdentityRole<int>, int>(options)
{
    public DbSet<Event> Events => Set<Event>();
    public DbSet<Category> Categories => Set<Category>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Status).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Description).HasMaxLength(5000);
            entity.Property(e => e.Format).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.TimeZone).HasMaxLength(64);
            entity.Property(e => e.VenueName).HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(500);
            entity.Property(e => e.VirtualUrl).HasMaxLength(2000);
            entity.Property(e => e.Visibility).HasConversion<string>().HasMaxLength(20);
            entity.HasOne(e => e.Organizer).WithMany().HasForeignKey(e => e.OrganizerId);
            entity.HasOne(e => e.Category).WithMany().HasForeignKey(e => e.CategoryId).OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.OrganizerId, e.StartsAtUtc });
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.Property(c => c.Name).HasMaxLength(100);
            entity.Property(c => c.Slug).HasMaxLength(100);
            entity.HasIndex(c => c.Slug).IsUnique();
            entity.HasData(
                new Category { Id = 1, Name = "Birthday", Slug = "birthday", SortOrder = 1 },
                new Category { Id = 2, Name = "Wedding", Slug = "wedding", SortOrder = 2 },
                new Category { Id = 3, Name = "Baby Shower", Slug = "baby-shower", SortOrder = 3 },
                new Category { Id = 4, Name = "Holiday", Slug = "holiday", SortOrder = 4 },
                new Category { Id = 5, Name = "Dinner Party", Slug = "dinner-party", SortOrder = 5 },
                new Category { Id = 6, Name = "Business", Slug = "business", SortOrder = 6 },
                new Category { Id = 7, Name = "Other", Slug = "other", SortOrder = 7 });
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.Property(u => u.Email).IsRequired();
            entity.Property(u => u.DisplayName).HasMaxLength(100);
            entity.Property(u => u.TimeZone).HasMaxLength(64);
        });

        modelBuilder.Entity<IdentityRole<int>>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasData(
                new IdentityRole<int> { Id = 1, Name = Models.Roles.Organizer, NormalizedName = "ORGANIZER", ConcurrencyStamp = "b6f1c2a4-3d6e-4f0a-9c1e-7a2d5b8e4f01" },
                new IdentityRole<int> { Id = 2, Name = Models.Roles.Administrator, NormalizedName = "ADMINISTRATOR", ConcurrencyStamp = "c7e2d3b5-4e7f-4a1b-8d2f-8b3e6c9f5a02" });
        });

        modelBuilder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
        modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
        modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
        modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");
        modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");
    }
}
