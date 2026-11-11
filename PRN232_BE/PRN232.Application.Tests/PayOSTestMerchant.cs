using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using PayOS;
using PayOS.Models.V2.PaymentRequests;
using System.Text.Json.Serialization;
using Xunit;

namespace PRN232.Application.Tests;

// An in-memory merchant wire endpoint. No request can reach a bank or real payOS host.
internal sealed class PayOSTestMerchant : HttpMessageHandler
{
    internal const string Key = "b12-test-only-checksum-key";
    internal const string LinkId = "b12-test-link";
    internal long OrderCode;
    internal long Amount;
    internal string Status = "PENDING";
    internal int Creates;
    internal int Cancels;
    internal int Gets;
    internal bool LoseCreateResponse;
    internal string? CheckoutFailure;
    private readonly PayOSClient _crypto = new("test-client", "test-api", Key);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Assert.Equal("payos.test.invalid", request.RequestUri!.Host);
        Assert.Equal("test-client", request.Headers.GetValues("x-client-id").Single());
        Assert.Equal("test-api", request.Headers.GetValues("x-api-key").Single());
        if (request.Method == HttpMethod.Post && request.RequestUri.AbsolutePath == "/v2/payment-requests")
        {
            var input = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var canonical = string.Join("&", new[] { "amount", "cancelUrl", "description", "orderCode", "returnUrl" }
                .Select(key => key + "=" + input.GetProperty(key).ToString()));
            Assert.Equal(Sign(canonical), input.GetProperty("signature").GetString());
            Assert.Equal(0, Interlocked.Exchange(ref Creates, 1));
            OrderCode = input.GetProperty("orderCode").GetInt64();
            Amount = input.GetProperty("amount").GetInt64();
            if (LoseCreateResponse) throw new TaskCanceledException("Test: provider saved the order but its creation reply was lost.");
            return Response(new CreatePaymentLinkResponse { Bin = "970422", AccountNumber = "0000000000", AccountName = "B12 TEST",
                Amount = Amount, Description = "HOADON", OrderCode = OrderCode, Currency = "VND",
                PaymentLinkId = LinkId, Status = Enum.Parse<PaymentLinkStatus>(Status, true), CheckoutUrl = "https://pay.payos.vn/web/" + LinkId, QrCode = "TEST-QR" });
        }
        Assert.Equal("/v2/payment-requests/" + OrderCode + (request.Method == HttpMethod.Post ? "/cancel" : ""), request.RequestUri.AbsolutePath);
        if (request.Method == HttpMethod.Post) { Cancels++; if (Status == "PENDING") Status = "CANCELLED"; }
        else { Assert.Equal(HttpMethod.Get, request.Method); Gets++; }
        return Response(new PaymentLink { Id = LinkId, OrderCode = OrderCode, Amount = Amount,
            AmountPaid = Status == "PAID" ? Amount : 0, AmountRemaining = Status == "PAID" ? 0 : Amount,
            Status = Enum.Parse<PaymentLinkStatus>(Status, true), CreatedAt = DateTime.UtcNow.AddMinutes(-1).ToString("O"),
            Transactions = Status == "PAID" ? [new PaymentTransaction { Reference = "B12-REF", Amount = Amount, TransactionDateTime = PaidTime }] : [] });
    }

    internal string PaidTime = "";
    internal void Pay()
    {
        if (Status == "PAID" && PaidTime.Length > 0) return;
        Status = "PAID";
        PaidTime = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Asia/Ho_Chi_Minh"))
            .ToString("yyyy-MM-dd HH:mm:ss.fff", CultureInfo.InvariantCulture);
    }

    internal object Webhook(bool tampered = false, bool additional = false)
    {
        var data = new SortedDictionary<string, object>(StringComparer.Ordinal)
        {
            ["orderCode"] = OrderCode, ["amount"] = Amount, ["description"] = "HOADON", ["accountNumber"] = "0000000000",
            ["reference"] = additional ? "B12-EXTRA-REF" : "B12-REF", ["transactionDateTime"] = PaidTime, ["currency"] = "VND",
            ["paymentLinkId"] = LinkId, ["code"] = "00", ["desc"] = "success", ["counterAccountBankId"] = "",
            ["counterAccountBankName"] = "", ["counterAccountName"] = "", ["counterAccountNumber"] = "",
            ["virtualAccountName"] = "", ["virtualAccountNumber"] = ""
        };
        var canonical = string.Join("&", data.Select(pair => pair.Key + "=" + Convert.ToString(pair.Value, CultureInfo.InvariantCulture)));
        return new { code = "00", desc = "success", success = true, data, signature = tampered ? "invalid" : Sign(canonical) };
    }

    private static string Sign(string canonical) => Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(Key), Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
    private HttpResponseMessage Response(object data) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(new { code = "00", desc = "success", data, signature = _crypto.Crypto.CreateSignatureFromObject(data, Key) },
            options: new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) } })
    };
}
