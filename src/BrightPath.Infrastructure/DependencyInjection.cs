using BrightPath.Application.Abstractions;
using BrightPath.Domain;
using BrightPath.Infrastructure.Persistence;
using BrightPath.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace BrightPath.Infrastructure;

public static class DependencyInjection
{
    /// <summary>The DbContext on Postgres and the Application ports. Needs a <c>BookingPolicy</c> registered.</summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<BrightPathDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IBookingLocks, PostgresBookingLocks>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IBookingChangeRepository, BookingChangeRepository>();
        services.AddScoped<ITutorRepository, TutorRepository>();
        services.AddScoped<IRoomRepository, RoomRepository>();
        services.AddScoped<IStudentRepository, StudentRepository>();
        services.AddScoped<ISessionReader, SessionReader>();
        return services;
    }

    /// <summary>
    /// Creates the database on first run, applies any pending migrations, then loads the exported week
    /// if <paramref name="seed"/> is on and the database is still empty.
    /// </summary>
    public static async Task InitialiseDatabaseAsync(this IServiceProvider services, bool seed, ILogger logger)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrightPathDbContext>();
        await db.Database.MigrateAsync();

        if (seed)
        {
            await SeedLoader.SeedAsync(db, scope.ServiceProvider.GetRequiredService<BookingPolicy>(), logger);
        }
    }
}
