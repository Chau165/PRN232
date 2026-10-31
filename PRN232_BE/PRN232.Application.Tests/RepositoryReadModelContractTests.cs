using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using Xunit;

namespace PRN232.Application.Tests;

public class RepositoryReadModelContractTests
{
    [Fact]
    public void Repositories_do_not_return_api_dtos()
    {
        var apiDtoNamespace = typeof(UtilityReadingDto).Namespace;
        var dtoReturningMethods = typeof(IUtilityReadingRepository).Assembly
            .GetTypes()
            .Where(type => type.IsInterface && type.Name.EndsWith("Repository", StringComparison.Ordinal))
            .SelectMany(repository => repository.GetMethods()
                .Where(method => GetNestedTypes(method.ReturnType)
                    .Any(type => type.Namespace == apiDtoNamespace))
                .Select(method => $"{repository.Name}.{method.Name}"))
            .ToArray();

        Assert.Empty(dtoReturningMethods);
    }

    private static IEnumerable<Type> GetNestedTypes(Type type)
    {
        yield return type;
        foreach (var argument in type.GetGenericArguments())
        {
            foreach (var nestedType in GetNestedTypes(argument))
            {
                yield return nestedType;
            }
        }
    }
}
