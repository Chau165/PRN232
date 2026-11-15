using FluentValidation;
using PRN232.Api.Controllers;
using PRN232.Application.Validation;

namespace PRN232.Api.Validation;

// Tạo/sửa khu trọ nhận multipart form (có ảnh) nên tham số action là PropertyWriteForm của tầng Api,
// không phải DTO của Application. Quy tắc dùng chung nằm ở PropertyRules. Tỉnh/phường vẫn do
// PropertyService đối chiếu với danh mục hành chính.
public sealed class PropertyWriteFormValidator : AbstractValidator<PropertyWriteForm>
{
    public PropertyWriteFormValidator()
    {
        RuleFor(form => form.PropertyName).ValidPropertyName();
        RuleFor(form => form.AddressText).ValidStreetAddress();
        RuleFor(form => form.Description).ValidDescription();
    }
}
