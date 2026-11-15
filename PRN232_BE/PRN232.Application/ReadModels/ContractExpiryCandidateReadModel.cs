namespace PRN232.Application.ReadModels;

// ManagerIds: quản lý đang được phân công cho khu trọ và tài khoản còn hoạt động.
public sealed record ContractExpiryCandidateReadModel(
    int ContractId,
    int CustomerId,
    string RoomCode,
    DateOnly EndDate,
    IReadOnlyList<int> ManagerIds);
