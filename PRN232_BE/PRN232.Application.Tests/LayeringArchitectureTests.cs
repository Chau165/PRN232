using System.Reflection;
using Microsoft.EntityFrameworkCore;
using PRN232.Api.Controllers;
using Xunit;

namespace PRN232.Application.Tests;

// N-layer rule: the Api layer (controllers, hubs, Api services) talks to Application services only.
// It must never depend on a repository or on the DbContext directly.
public sealed class LayeringArchitectureTests
{
    [Fact]
    public void Api_layer_does_not_depend_on_repositories_or_DbContext()
    {
        var apiAssembly = typeof(HealthChecksController).Assembly;

        var violations = apiAssembly.GetTypes()
            .Where(type => type.IsClass && !type.IsAbstract && type.Namespace?.StartsWith("PRN232.Api") == true)
            .SelectMany(type => type
                .GetConstructors(BindingFlags.Public | BindingFlags.Instance)
                .SelectMany(constructor => constructor.GetParameters())
                .Where(parameter => IsForbiddenDependency(parameter.ParameterType))
                .Select(parameter => $"{type.FullName} <- {parameter.ParameterType.Name}"))
            .Distinct()
            .ToList();

        Assert.True(violations.Count == 0, "Api layer must go through services:\n" + string.Join("\n", violations));
    }

    private static bool IsForbiddenDependency(Type type)
    {
        return typeof(DbContext).IsAssignableFrom(type)
            || (type.IsInterface && type.Name.StartsWith('I') && type.Name.EndsWith("Repository", StringComparison.Ordinal));
    }
}
