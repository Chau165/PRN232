namespace PRN232.Application.ReadModels;

// IdentityNumber đã được giải mã khi đọc (EF converter); chỉ dùng trong service để che hoặc trả cho người có quyền.
public sealed record ContractOccupantReadModel(
    int ContractOccupantId, string FullName, string? PhoneNumber, string IdentityNumber, DateOnly MoveInDate,
    DateOnly? MoveOutDate, string? Note, string AddedByName, DateTime CreatedAtUtc);
