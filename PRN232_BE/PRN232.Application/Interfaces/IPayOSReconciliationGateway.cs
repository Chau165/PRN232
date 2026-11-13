namespace PRN232.Application.Interfaces;

public record PayOSRemoteTransfer(string Reference, decimal Amount, string TransactionDateTime);
public record PayOSRemoteSnapshot(long OrderCode, string LinkId, decimal Amount, decimal AmountPaid,
    decimal AmountRemaining, string Status, IReadOnlyList<PayOSRemoteTransfer> Transactions);

public interface IPayOSReconciliationGateway
{
    Task<PayOSRemoteSnapshot?> GetAsync(long orderCode, CancellationToken cancellationToken);
}
