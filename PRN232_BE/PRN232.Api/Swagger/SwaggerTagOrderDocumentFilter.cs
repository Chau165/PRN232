using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PRN232.Api.Swagger;

public sealed class SwaggerTagOrderDocumentFilter : IDocumentFilter
{
    private static readonly string[] PriorityTags = ["HealthChecks", "Auth", "Users"];

    public void Apply(OpenApiDocument swaggerDoc, DocumentFilterContext context)
    {
        var tagNames = swaggerDoc.Paths.Values
            .SelectMany(path => path.Operations.Values)
            .SelectMany(operation => operation.Tags)
            .Select(tag => tag.Name)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        swaggerDoc.Tags = PriorityTags
            .Where(tagNames.Contains)
            .Concat(tagNames.Except(PriorityTags, StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.OrdinalIgnoreCase))
            .Select(name => new OpenApiTag { Name = name })
            .ToList();
    }
}
