namespace PRN232.Application.DTOs;

// Rebuild 3.5. Quản lý chọn tình trạng từng thiết bị của phòng; tên, số lượng, giá trị bồi thường do BE
// tự chụp lại từ danh sách thiết bị (client không gửi), để không ai sửa được giá đã thống nhất.
public record SaveHandoverDto(DateOnly HandoverDate, string? Note, IReadOnlyList<SaveHandoverItemDto> Items);

// ChargeAmount chỉ dùng ở biên bản trả phòng, với thiết bị Damaged/Missing.
public record SaveHandoverItemDto(int EquipmentId, string Condition, decimal? ChargeAmount = null, string? Note = null);

public record HandoverItemDto(
    int HandoverItemId,
    int? EquipmentId,
    string EquipmentName,
    int Quantity,
    decimal CompensationValue,
    string Condition,
    decimal ChargeAmount,
    string? Note,
    string? MoveInCondition);

public record HandoverDto(
    int HandoverRecordId,
    string Kind,
    DateOnly HandoverDate,
    string? Note,
    string RecordedByName,
    DateTime RecordedAtUtc,
    decimal TotalCharge,
    IReadOnlyList<HandoverItemDto> Items);

// Thiết bị hiện có của phòng: FE dùng để dựng form (mỗi thiết bị một dòng chọn tình trạng).
public record HandoverEquipmentDto(int EquipmentId, string Name, int Quantity, decimal CompensationValue);

public record ContractHandoversDto(
    int ContractId,
    HandoverDto? MoveIn,
    HandoverDto? MoveOut,
    IReadOnlyList<HandoverEquipmentDto> RoomEquipments);
