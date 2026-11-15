namespace PRN232.Application.DTOs;

public sealed record ContractEvidenceUpload(string FileName, string ContentType, byte[] Content);

// Url là link tạm (1 giờ) tới bucket private; null với file đã bị Admin ẩn (trừ khi người xem là Admin).
public record ContractEvidenceFileDto(
    int ContractEvidenceFileId,
    string Kind,
    string FileName,
    string ContentType,
    long SizeBytes,
    string UploadedByName,
    string UploadedByRole,
    DateTime UploadedAtUtc,
    bool CanDelete,
    string? Url,
    bool IsHidden,
    string? HiddenReason);

// MoveInUploadDeadline: ngày cuối (giờ VN) còn upload/xóa ảnh nhận phòng.
public record ContractEvidenceDto(
    int ContractId,
    DateOnly MoveInUploadDeadline,
    bool CanUploadMoveIn,
    bool CanUploadMoveOut,
    bool CanUploadSignedContract,
    IReadOnlyList<ContractEvidenceFileDto> Files);

public record HideContractEvidenceDto(string Reason);
