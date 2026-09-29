using BrightPath.Api.Data;
using BrightPath.Api.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BrightPath.Api.Tests;

[Collection(ApiCollection.Name)]
public sealed class HealthEndpointTests(BrightPathApiFactory factory)
{
    [Fact]
    public async Task Health_reports_healthy_when_the_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public void The_tests_never_run_on_the_dev_database()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BrightPathDbContext>();

        Assert.StartsWith(BrightPathApiFactory.DatabasePrefix, db.Database.GetDbConnection().Database);
    }
}
