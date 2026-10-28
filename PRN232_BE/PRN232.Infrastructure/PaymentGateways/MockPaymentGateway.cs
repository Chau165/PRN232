using PRN232.Application.Interfaces;

namespace PRN232.Infrastructure.PaymentGateways;

// Cổng thanh toán giả lập cho đồ án: không gọi VNPay/MoMo thật nên mọi giao dịch đều thành công.
// Muốn dùng cổng thật thì viết class khác implement IPaymentGateway và đổi dòng đăng ký trong Program.cs.
public class MockPaymentGateway : IPaymentGateway
{
    public Task<bool> ChargeAsync(string paymentMethod, decimal amount) => Task.FromResult(true);
}
