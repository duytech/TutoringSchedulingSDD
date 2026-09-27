using BrightPath.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Data;

public sealed class BrightPathDbContext(DbContextOptions<BrightPathDbContext> options) : DbContext(options)
{
    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Attendee> Attendees => Set<Attendee>();
    public DbSet<BookingChange> BookingChanges => Set<BookingChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BrightPathDbContext).Assembly);
}
