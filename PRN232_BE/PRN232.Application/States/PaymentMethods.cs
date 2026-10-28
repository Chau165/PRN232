namespace PRN232.Application.States;

public static class PaymentMethods
{
    public const string VNPay = "VNPay";
    public const string MoMo = "MoMo";

    public static bool IsValid(string method) => method is VNPay or MoMo;
}
