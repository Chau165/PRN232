using System.Globalization;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace PRN232.Api.Validation;

// Map coordinates use a decimal point regardless of the server/request language.
public sealed class CoordinateModelBinder : IModelBinder
{
    public Task BindModelAsync(ModelBindingContext context)
    {
        var value = context.ValueProvider.GetValue(context.ModelName);
        if (value == ValueProviderResult.None || string.IsNullOrWhiteSpace(value.FirstValue))
        {
            context.Result = ModelBindingResult.Success(null);
            return Task.CompletedTask;
        }
        context.ModelState.SetModelValue(context.ModelName, value);
        if (decimal.TryParse(value.FirstValue, NumberStyles.Float, CultureInfo.InvariantCulture, out var coordinate))
            context.Result = ModelBindingResult.Success((decimal?)coordinate);
        else context.ModelState.TryAddModelError(context.ModelName, "Tọa độ phải là số, dùng dấu chấm cho phần thập phân.");
        return Task.CompletedTask;
    }
}
