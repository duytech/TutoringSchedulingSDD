using BrightPath.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace BrightPath.Api.Tests;

public sealed class SchemaTests
{
    [Fact]
    public void Migrations_match_the_model()
    {
        // Same provider setup as Program.cs. No connection is opened.
        var options = new DbContextOptionsBuilder<BrightPathDbContext>()
            .UseNpgsql("Host=unused")
            .UseSnakeCaseNamingConvention()
            .Options;
        using var context = new BrightPathDbContext(options);

        Assert.False(
            context.Database.HasPendingModelChanges(),
            "The model has changes that are not in a migration. Run 'dotnet ef migrations add'.");
    }
}
