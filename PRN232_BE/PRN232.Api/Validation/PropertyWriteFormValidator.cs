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
        RuleFor(form => form.Latitude).Must(value => !value.HasValue || PRN232.Application.Policies.PropertyCoordinatePolicy.IsLatitudeValid(value.Value))
            .WithMessage("Vĩ độ phải trong khoảng 6,5 đến 24.");
        RuleFor(form => form.Longitude).Must(value => !value.HasValue || PRN232.Application.Policies.PropertyCoordinatePolicy.IsLongitudeValid(value.Value))
            .WithMessage("Kinh độ phải trong khoảng 102 đến 117,5.");
        RuleFor(form => form.Latitude).NotNull().When(form => form.Longitude.HasValue).WithMessage("Cần gửi cả vĩ độ và kinh độ.");
        RuleFor(form => form.Longitude).NotNull().When(form => form.Latitude.HasValue).WithMessage("Cần gửi cả vĩ độ và kinh độ.");
    }
}
