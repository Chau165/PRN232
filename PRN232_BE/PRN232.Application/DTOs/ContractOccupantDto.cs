namespace PRN232.Application.DTOs;

// Rebuild 4.1. IdentityNumber: CCCD 12 chữ số (bắt buộc, để chủ trọ đăng ký tạm trú).
public record AddContractOccupantDto(string FullName, string? PhoneNumber, string IdentityNumber, DateOnly MoveInDate,
    string? Note = null);

public record RecordOccupantMoveOutDto(DateOnly MoveOutDate);

// MaskedIdentityNumber luôn là "********1234": API danh sách KHÔNG BAO GIỜ trả đủ số CCCD.
public record ContractOccupantDto(
    int ContractOccupantId,
    string FullName,
    string? PhoneNumber,
    string MaskedIdentityNumber,
    DateOnly MoveInDate,
    DateOnly? MoveOutDate,
    bool IsActive,
    string? Note,
    string AddedByName,
    DateTime CreatedAtUtc);

// CurrentCount tính cả người đứng tên. CanManage: được thêm người / ghi ngày dọn ra.
// CanRevealIdentity: được bấm "con mắt" xem đủ số CCCD (quản lý phụ trách, Admin là chủ khu trọ).
public record ContractOccupantsDto(
    int ContractId,
    string HolderName,
    int MaxOccupants,
    int CurrentCount,
    bool CanManage,
    bool CanRevealIdentity,
    IReadOnlyList<ContractOccupantDto> Occupants);

public record RevealedIdentityDto(int ContractOccupantId, string FullName, string IdentityNumber);
