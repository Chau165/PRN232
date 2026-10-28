namespace PRN232.Application.States;

public static class PaymentMethods
{
    public const string VNPay = "VNPay";
    public const string MoMo = "MoMo";
    // Chỉ dùng khi quản lý ghi nhận khách trả tiền mặt; khách không chọn được khi tự thanh toán.
    public const string Cash = "Cash";

    // Các phương thức khách được chọn khi tự thanh toán trực tuyến (qua cổng giả lập).
    public static bool IsValid(string method) => method is VNPay or MoMo;
}
