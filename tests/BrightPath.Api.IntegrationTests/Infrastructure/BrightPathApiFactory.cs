using BrightPath.Api.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace BrightPath.Api.IntegrationTests.Infrastructure;

/// <summary>
/// The API on a throwaway database (<c>brightpath_test_&lt;guid&gt;</c>) on the local Postgres. Startup migrates it
/// and seeds the export once per run; the database is dropped when the run ends. The dev database is never touched.
/// </summary>
public sealed class BrightPathApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string DatabasePrefix = "brightpath_test_";

    public BrightPathApiFactory()
    {
        var builder = new NpgsqlConnectionStringBuilder(BaseConnectionString())
        {
            Database = $"{DatabasePrefix}{Guid.NewGuid():N}",
        };
        ConnectionString = builder.ConnectionString;
        DatabaseName = builder.Database;
    }

    public string ConnectionString { get; }

    public string DatabaseName { get; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        // Program.cs reads the connection string before Build(), so it has to be a host setting.
        builder.UseSetting("ConnectionStrings:BrightPath", ConnectionString);
    }

    public Task InitializeAsync()
    {
        // Starts the host: migrate and seed run once, before the first test.
        _ = Services;
        return Task.CompletedTask;
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        NpgsqlConnection.ClearAllPools();

        var admin = new NpgsqlConnectionStringBuilder(ConnectionString) { Database = "postgres" };
        await using var connection = new NpgsqlConnection(admin.ConnectionString);
        await connection.OpenAsync();
        await using var drop = new NpgsqlCommand($"""DROP DATABASE IF EXISTS "{DatabaseName}" WITH (FORCE)""", connection);
        await drop.ExecuteNonQueryAsync();
    }

    /// <summary>A context of its own on the test database, outside the API. The race tests hold transactions with it.</summary>
    public BrightPathDbContext OpenDb() =>
        new(new DbContextOptionsBuilder<BrightPathDbContext>()
            .UseNpgsql(ConnectionString)
            .UseSnakeCaseNamingConvention()
            .Options);

    public async Task<Guid> StudentId(string name)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrightPathDbContext>();
        return await db.Students.Where(s => s.Name == name).Select(s => s.Id).SingleAsync();
    }

    /// <summary>Read the same way the app reads it, so the password in user-secrets is used.</summary>
    private static string BaseConnectionString()
    {
        var config = new ConfigurationBuilder()
            .AddJsonFile(Path.Combine(ApiDirectory(), "appsettings.Development.json"), optional: true)
            .AddUserSecrets<Program>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        return config.GetConnectionString("BrightPath")
            ?? throw new InvalidOperationException(
                "Connection string 'BrightPath' is not set. The tests create their own database on the same server.");
    }

    private static string ApiDirectory()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var api = Path.Combine(dir.FullName, "src", "BrightPath.Api");
            if (Directory.Exists(api))
            {
                return api;
            }
        }

        throw new InvalidOperationException("Cannot find src/BrightPath.Api above the test output directory.");
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<BrightPathApiFactory>
{
    public const string Name = "api";
}
