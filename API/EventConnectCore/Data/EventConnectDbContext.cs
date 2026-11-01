using EventConnectCore.Models;
using Microsoft.EntityFrameworkCore;

namespace EventConnectCore.Data;

public class EventConnectDbContext(DbContextOptions<EventConnectDbContext> options) : DbContext(options)
{
    public DbSet<Event> Events => Set<Event>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Event>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(300);
        });
    }
}
