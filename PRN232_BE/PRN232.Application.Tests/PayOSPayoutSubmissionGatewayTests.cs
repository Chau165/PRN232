using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;
using PayOS;
using PayOS.Models.V1.Payouts;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;
using PRN232.Infrastructure.PaymentGateways;
using Xunit;

namespace PRN232.Application.Tests;

public class PayOSPayoutSubmissionGatewayTests
{
    private static readonly PayoutSubmissionRequest Input = new("refund_12_abc", "1234567890abcdef1234567890abcdef", 2500000,
        "970422", "001234567890", "Hoan coc booking 12");

    [Fact]
    public async Task Real_sdk_sends_persisted_idempotency_header_and_signed_snapshot()
    {
        using var merchant = new Merchant();
        PayoutSubmissionReceipt? result = null;
        var error = await Record.ExceptionAsync(async () => result = await Gateway(merchant).SubmitAsync(Input, default));
        Assert.Null(merchant.WireError);
        Assert.Null(error);
        Assert.Equal("provider-payout", result!.ProviderPayoutId); Assert.Equal("provider-transaction", result.ProviderTransactionId);
        Assert.Equal("Processing", result.TransactionState); Assert.Equal(1, merchant.Calls);
    }

    [Theory]
    [InlineData("amount")]
    [InlineData("account")]
    [InlineData("reference")]
    [InlineData("multiple")]
    [InlineData("signature")]
    [InlineData("timeout")]
    [InlineData("server")]
    public async Task Uncertain_or_mismatched_responses_never_become_success_and_sdk_does_not_retry(string failure)
    {
        using var merchant = new Merchant { Failure = failure };
        await Assert.ThrowsAsync<PayOSPayoutGatewayException>(() => Gateway(merchant).SubmitAsync(Input, default));
        Assert.Null(merchant.WireError);
        Assert.Equal(1, merchant.Calls);
    }

    [Fact]
    public async Task Disabled_dispatch_and_invalid_input_never_call_provider()
    {
        using var merchant = new Merchant();
        await Assert.ThrowsAsync<PayOSPayoutDisabledException>(() => Gateway(merchant, false).SubmitAsync(Input, default));
        await Assert.ThrowsAsync<PayOSPayoutValidationException>(() => Gateway(merchant).SubmitAsync(Input with { IdempotencyKey = "" }, default));
        await Assert.ThrowsAsync<PayOSPayoutValidationException>(() => Gateway(merchant).SubmitAsync(Input with { Amount = 1.5m }, default));
        Assert.Equal(0, merchant.Calls);
    }

    private static PayOSPayoutSubmissionGateway Gateway(Merchant merchant, bool dispatch = true) => new(
        new PayOSPayoutClient(new PayOSClient(new PayOSOptions { ClientId = "payout-client", ApiKey = "payout-api", ChecksumKey = "payout-checksum",
            BaseUrl = "https://payout.test.invalid", MaxRetries = 0, HttpClient = new HttpClient(merchant, false) })),
        Options.Create(new PayOSPayoutSettings { Enabled = true, DispatchEnabled = dispatch }));

    private sealed class Merchant : HttpMessageHandler
    {
        public int Calls;
        public string? Failure;
        public Exception? WireError;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try { return await HandleAsync(request, cancellationToken); }
            catch (Exception exception) when (exception is not TimeoutException) { WireError = exception; throw; }
        }
        private async Task<HttpResponseMessage> HandleAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            Assert.Equal("payout.test.invalid", request.RequestUri!.Host); Assert.Equal("/v1/payouts/", request.RequestUri.AbsolutePath);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal(Input.IdempotencyKey, request.Headers.GetValues("x-idempotency-key").Single());
            Assert.Equal("payout-client", request.Headers.GetValues("x-client-id").Single());
            var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
            var crypto = new PayOSClient("payout-client", "payout-api", "payout-checksum");
            Assert.Equal(crypto.Crypto.CreateSignature("payout-checksum", body, new PayOS.Crypto.CryptoSignatureOptions()), request.Headers.GetValues("x-signature").Single());
            Assert.Equal(Input.ReferenceId, body.GetProperty("referenceId").GetString());
            Assert.Equal(Input.Amount, body.GetProperty("amount").GetDecimal());
            Assert.Equal(Input.RecipientAccountNumber, body.GetProperty("toAccountNumber").GetString());
            if (Failure == "timeout") throw new TimeoutException("private-provider-response");
            if (Failure == "server") return new(HttpStatusCode.InternalServerError) { Content = JsonContent.Create(new { code = "error", desc = "private" }) };
            var transaction = new PayoutTransaction { Id = "provider-transaction", ReferenceId = Input.ReferenceId,
                Amount = Failure == "amount" ? 1 : (long)Input.Amount, ToBin = Input.RecipientBankBin,
                ToAccountNumber = Failure == "account" ? "000" : Input.RecipientAccountNumber, State = PayoutTransactionState.Processing };
            var data = new Payout { Id = "provider-payout", ReferenceId = Failure == "reference" ? "other" : Input.ReferenceId,
                ApprovalState = PayoutApprovalState.Processing, Transactions = Failure == "multiple" ? [transaction, transaction] : [transaction] };
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { code = "00", desc = "success", data },
                options: new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) } }) };
            response.Headers.Add("x-signature", Failure == "signature" ? "invalid" :
                crypto.Crypto.CreateSignature("payout-checksum", data, new PayOS.Crypto.CryptoSignatureOptions()));
            return response;
        }
    }
}
