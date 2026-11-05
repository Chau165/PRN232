using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IPropertyImageStorage
{
    Task<string> UploadAsync(
        PropertyImageInput image,
        CancellationToken cancellationToken = default);

    Task DeleteAsync(
        string imageUrl,
        CancellationToken cancellationToken = default);
}
