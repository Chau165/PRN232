namespace PRN232.Application.ReadModels;

public enum ContractMutationStatus
{
    Updated,
    NotFound,
    Forbidden,
    Conflict
}

public sealed record ContractMutationResult(
    ContractMutationStatus Status,
    ContractReadModel? Contract = null,
    decimal OutstandingDebt = 0m);
