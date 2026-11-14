using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PRN232.Api.Validation;

// Chạy mọi validator FluentValidation đã đăng ký cho tham số của action, TRƯỚC khi vào action.
// Có lỗi thì trả 400 ValidationProblemDetails gồm TẤT CẢ lỗi theo từng field, key là tên field
// dạng camelCase giống JSON client gửi lên (FE: apiClient.ts đọc "errors" để hiện dưới ô nhập).
// "detail" giữ câu lỗi đầu tiên để các màn hình chỉ hiện một câu báo lỗi vẫn chạy như cũ.
public sealed class FluentValidationFilter : IAsyncActionFilter
{
    public const string Title = "Dữ liệu không hợp lệ.";

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var failures = new List<FluentValidation.Results.ValidationFailure>();
        foreach (var argument in context.ActionArguments.Values)
        {
            if (argument is null)
            {
                continue;
            }

            var validatorType = typeof(IValidator<>).MakeGenericType(argument.GetType());
            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var result = await validator.ValidateAsync(
                new ValidationContext<object>(argument), context.HttpContext.RequestAborted);
            failures.AddRange(result.Errors);
        }

        if (failures.Count == 0)
        {
            await next();
            return;
        }

        var errors = failures
            .GroupBy(failure => ToJsonFieldName(failure.PropertyName))
            .ToDictionary(group => group.Key, group => group.Select(failure => failure.ErrorMessage).ToArray());
        context.Result = new BadRequestObjectResult(new ValidationProblemDetails(errors)
        {
            Title = Title,
            Detail = failures[0].ErrorMessage,
            Status = StatusCodes.Status400BadRequest
        });
    }

    // "AreaSqm" -> "areaSqm", "Items[0].UnitPrice" -> "items[0].unitPrice".
    private static string ToJsonFieldName(string propertyName) =>
        string.Join('.', propertyName.Split('.').Select(JsonNamingPolicy.CamelCase.ConvertName));
}
