using PRN232.Application.DTOs;

namespace PRN232.Application.Interfaces;

public interface IRoomImageStorage
{
    Task<string> UploadAsync(RoomImageInput image, CancellationToken cancellationToken = default);
    Task DeleteAsync(string imageUrl, CancellationToken cancellationToken = default);
}
