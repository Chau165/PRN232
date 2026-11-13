using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PRN232.Domain.Entities;

namespace PRN232.Application.Policies;

public static class PayOSReceiptFactory
{
    public static string Fingerprint(PaymentWebhookReceipts receipt) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
        {
            receipt.OrderCode, receipt.PaymentLinkId, receipt.Amount, receipt.Currency,
            Code = receipt.DataCode, receipt.Reference, receipt.TransactionDateTime
        }))));
}
