using EventConnectCore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace EventConnectCore.Data;

public class EventConnectDbContext(DbContextOptions<EventConnectDbContext> options)
    : IdentityDbContext<User, IdentityRole<int>, int>(options)
{
    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(300);
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
