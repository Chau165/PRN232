namespace PRN232.Application.Interfaces;

public interface IPaymentGateway
{
    Task<bool> ChargeAsync(string paymentMethod, decimal amount);
}
