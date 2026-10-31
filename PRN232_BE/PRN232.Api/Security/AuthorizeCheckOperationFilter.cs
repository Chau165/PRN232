using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi.Models;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace PRN232.Api.Security;

public sealed class AuthorizeCheckOperationFilter : IOperationFilter
{
    public void Apply(OpenApiOperation operation, OperationFilterContext context)
    {
        var methodAttributes = context.MethodInfo.GetCustomAttributes(true);
        if (methodAttributes.OfType<AllowAnonymousAttribute>().Any())
        {
            return;
        }

        object[] controllerAttributes = [];
        var controllerType = context.MethodInfo.DeclaringType;
        if (controllerType is not null)
        {
            controllerAttributes = controllerType.GetCustomAttributes(true);
        }
        if (!methodAttributes.OfType<AuthorizeAttribute>().Any() && !controllerAttributes.OfType<AuthorizeAttribute>().Any())
        {
            return;
        }

        operation.Security = new List<OpenApiSecurityRequirement>
        {
            new OpenApiSecurityRequirement()
            {
                [new OpenApiSecurityScheme
                {
                    Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
                }] = Array.Empty<string>()
            }
        };
    }
}
