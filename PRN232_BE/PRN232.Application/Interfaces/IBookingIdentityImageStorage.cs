namespace PRN232.Application.Interfaces;

public record BookingIdentityUpload(byte[] Content, string ContentType);
public record StoredBookingIdentityImage(string ObjectPath, string ContentType, long SizeBytes);

// Infrastructure only. A future application service MUST authorize customer/reviewer before each call.
public interface IBookingIdentityImageStorage
{
    Task<StoredBookingIdentityImage> UploadAsync(int applicationId, int personId, string side,
        BookingIdentityUpload image, CancellationToken cancellationToken = default);
    Task<string> GetReadUrlAsync(string objectPath, CancellationToken cancellationToken = default);
    Task DeleteAsync(string objectPath, CancellationToken cancellationToken = default);
}
