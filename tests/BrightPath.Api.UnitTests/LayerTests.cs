using System.Reflection;
using BrightPath.Application.Sessions;
using BrightPath.Domain;

namespace BrightPath.Api.UnitTests;

/// <summary>Dependencies point inward: Api → Infrastructure → Application → Domain.</summary>
public sealed class LayerTests
{
    [Fact]
    public void Domain_knows_no_framework_and_no_other_layer()
    {
        Assert.Empty(ReferencesStartingWith(
            typeof(Session).Assembly,
            "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore", "BrightPath."));
    }

    [Fact]
    public void Application_knows_no_database_and_no_http()
    {
        Assert.Empty(ReferencesStartingWith(
            typeof(CreateSessionHandler).Assembly,
            "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore", "BrightPath.Infrastructure",
            "BrightPath.Api"));
    }

    private static List<string> ReferencesStartingWith(Assembly assembly, params string[] prefixes) =>
        assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => prefixes.Any(p => name.StartsWith(p, StringComparison.Ordinal)))
            .ToList();
}
