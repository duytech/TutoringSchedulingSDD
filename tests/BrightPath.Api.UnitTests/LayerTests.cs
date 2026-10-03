using System.Reflection;
using BrightPath.Application.Sessions;
using BrightPath.Common;
using BrightPath.Domain;

namespace BrightPath.Api.UnitTests;

/// <summary>Dependencies point inward: Api → Infrastructure → Application → Domain → Common.</summary>
public sealed class LayerTests
{
    [Fact]
    public void Common_knows_no_framework_and_no_layer()
    {
        Assert.Empty(ReferencesStartingWith(typeof(DateTimeUtils).Assembly, "Microsoft", "Npgsql", "BrightPath."));
    }

    [Fact]
    public void Domain_knows_no_framework_and_no_other_layer()
    {
        var references = ReferencesStartingWith(
            typeof(Session).Assembly,
            "Microsoft.EntityFrameworkCore", "Npgsql", "Microsoft.AspNetCore", "BrightPath.");

        Assert.Equal(["BrightPath.Common"], references);
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
