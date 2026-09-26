using Microsoft.AspNetCore.Mvc.Testing;

namespace BrightPath.Api.Tests;

public sealed class HealthEndpointTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Health_reports_healthy_when_the_database_is_reachable()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
