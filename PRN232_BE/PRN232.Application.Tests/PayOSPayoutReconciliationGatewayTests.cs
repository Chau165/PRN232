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

public class PayOSPayoutReconciliationGatewayTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Real_sdk_performs_signed_read_only_lookup_by_id_or_reference(bool byId)
    {
        using var merchant = new Merchant { ById = byId };
        var result = await Gateway(merchant).GetAsync(new("refund_12", byId ? "payout_12" : null), default);
        Assert.Null(merchant.WireError); Assert.NotNull(result); Assert.Equal("payout_12", result.ProviderPayoutId);
        Assert.Equal("txn_12", result.ProviderTransactionId); Assert.Equal("001234567890", result.RecipientAccountNumber);
        Assert.Equal("Succeeded", result.TransactionState); Assert.Equal(1, merchant.Calls);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("404")]
    public async Task Not_found_returns_no_evidence_of_failure(string failure)
    {
        using var merchant = new Merchant { Failure = failure };
        Assert.Null(await Gateway(merchant).GetAsync(new("refund_12", null), default)); Assert.Null(merchant.WireError);
    }

    [Theory]
    [InlineData("timeout")]
    [InlineData("server")]
    [InlineData("signature")]
    [InlineData("multiple")]
    [InlineData("truncated")]
    [InlineData("reference")]
    [InlineData("transaction")]
    public async Task Untrusted_or_ambiguous_results_are_sanitized_and_never_retried(string failure)
    {
        using var merchant = new Merchant { Failure = failure };
        var exception = await Assert.ThrowsAsync<PayOSPayoutGatewayException>(() => Gateway(merchant).GetAsync(new("refund_12", null), default));
        Assert.DoesNotContain("provider-private", exception.ToString()); Assert.Null(merchant.WireError); Assert.Equal(1, merchant.Calls);
    }

    [Fact]
    public async Task Disabled_gateway_invalid_identifiers_and_cancellation_do_not_call_provider()
    {
        using var merchant = new Merchant();
        await Assert.ThrowsAsync<PayOSPayoutDisabledException>(() => Gateway(merchant, false).GetAsync(new("refund_12", null), default));
        await Assert.ThrowsAsync<PayOSPayoutValidationException>(() => Gateway(merchant).GetAsync(new("refund_12", "../other"), default));
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Gateway(merchant).GetAsync(new("refund_12", null), cancellation.Token));
        Assert.Equal(0, merchant.Calls);
    }

    private static PayOSPayoutReconciliationGateway Gateway(Merchant merchant, bool enabled = true) => new(
        new PayOSPayoutClient(new PayOSClient(new PayOSOptions { ClientId = "payout-client", ApiKey = "payout-api", ChecksumKey = "payout-checksum",
            BaseUrl = "https://payout.test.invalid", MaxRetries = 0, HttpClient = new HttpClient(merchant, false) })),
        Options.Create(new PayOSPayoutSettings { Enabled = enabled, ReconciliationEnabled = true }));

    private sealed class Merchant : HttpMessageHandler
    {
        public int Calls;
        public bool ById;
        public string? Failure;
        public Exception? WireError;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            try
            {
                Assert.Equal(HttpMethod.Get, request.Method); Assert.Equal("payout.test.invalid", request.RequestUri!.Host);
                Assert.Equal("payout-client", request.Headers.GetValues("x-client-id").Single());
                Assert.Equal("payout-api", request.Headers.GetValues("x-api-key").Single());
                if (ById) Assert.Equal("/v1/payouts/payout_12", request.RequestUri.AbsolutePath.TrimEnd('/'));
                else
                {
                    Assert.Equal("/v1/payouts", request.RequestUri.AbsolutePath.TrimEnd('/'));
                    Assert.Contains("referenceId=refund_12", request.RequestUri.Query); Assert.Contains("limit=2", request.RequestUri.Query);
                }
                if (Failure == "timeout") throw new TimeoutException("provider-private");
                if (Failure is "404" or "server") return Task.FromResult(new HttpResponseMessage(Failure == "404" ? HttpStatusCode.NotFound : HttpStatusCode.InternalServerError)
                { Content = JsonContent.Create(new { code = "error", desc = "provider-private" }) });
                var transaction = new PayoutTransaction { Id = "txn_12", ReferenceId = Failure == "transaction" ? "other" : "refund_12", Amount = 2500000,
                    ToBin = "970422", ToAccountNumber = "001234567890", State = PayoutTransactionState.Succeeded };
                var payout = new Payout { Id = "payout_12", ReferenceId = Failure == "reference" ? "other" : "refund_12",
                    ApprovalState = PayoutApprovalState.Completed, Transactions = [transaction] };
                object data = ById ? payout : new PayoutListResponse { Payouts = Failure == "empty" ? [] : Failure == "multiple" ? [payout, payout] : [payout],
                    Pagination = new Pagination { Count = Failure == "empty" ? 0 : 1, Limit = 2, Offset = 0,
                        Total = Failure == "empty" ? 0 : Failure == "multiple" ? 2 : 1, HasMore = Failure == "truncated" } };
                var crypto = new PayOSClient("payout-client", "payout-api", "payout-checksum");
                var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(new { code = "00", desc = "success", data },
                    options: new JsonSerializerOptions(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper) } }) };
                response.Headers.Add("x-signature", Failure == "signature" ? "invalid" :
                    crypto.Crypto.CreateSignature("payout-checksum", data, new PayOS.Crypto.CryptoSignatureOptions()));
                return Task.FromResult(response);
            }
            catch (Exception exception) when (exception is not TimeoutException) { WireError = exception; throw; }
        }
    }
}
