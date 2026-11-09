namespace PRN232.Application.ReadModels;

public record RevenueBucketReadModel(int PropertyId, string PropertyName, DateOnly? Month, decimal Amount, int PaymentCount);
