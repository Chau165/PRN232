using PRN232.Application.DTOs;
namespace PRN232.Application.Interfaces;
public interface IPropertyLocationPreviewService
{
    Task<PropertyLocationPreviewDto> PreviewAsync(PropertyLocationPreviewRequest request, CancellationToken token);
}
