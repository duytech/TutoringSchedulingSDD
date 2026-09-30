using BrightPath.Domain;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Infrastructure.Persistence;

public sealed class BrightPathDbContext(DbContextOptions<BrightPathDbContext> options) : DbContext(options)
{
    public DbSet<Tutor> Tutors => Set<Tutor>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Attendee> Attendees => Set<Attendee>();
    public DbSet<BookingChange> BookingChanges => Set<BookingChange>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Needed by the EXCLUDE constraints: lets a GiST index compare plain columns with =.
        modelBuilder.HasPostgresExtension("btree_gist");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BrightPathDbContext).Assembly);
    }
}
