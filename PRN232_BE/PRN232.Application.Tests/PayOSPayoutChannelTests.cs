using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Hosting;
using PayOS;
using PRN232.Api.Configuration;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Settings;
using PRN232.Infrastructure.PaymentGateways;
using Xunit;

namespace PRN232.Application.Tests;

public class PayOSPayoutChannelTests
{
    private static readonly PayoutPreflightRequest Valid = new("refund-1", 100000, "970422", "00123456", "Hoan coc");

    [Fact]
    public void Disabled_channel_needs_no_credentials_or_client()
    {
        var services = Services(new());
        using var provider = services.BuildServiceProvider();
        Assert.False(provider.GetRequiredService<IOptions<PayOSPayoutSettings>>().Value.Enabled);
        Assert.IsType<DisabledPayOSPayoutGateway>(provider.GetRequiredService<IPayOSPayoutGateway>());
        Assert.Null(provider.GetService<PayOSPayoutClient>());
    }

    [Fact]
    public void Collection_keys_do_not_satisfy_payout_configuration()
    {
        using var provider = Services(new() { ["PayOSPayout:Enabled"] = "true", ["PayOS:ClientId"] = "collection",
            ["PayOS:ApiKey"] = "secret-collection", ["PayOS:ChecksumKey"] = "secret-checksum" }).BuildServiceProvider();
        var error = Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PayOSPayoutSettings>>().Value);
        Assert.DoesNotContain("secret-", error.Message);
        Assert.Equal(3, error.Failures.Count());
    }

    [Theory]
    [InlineData("", 15000)]
    [InlineData("your-key", 15000)]
    [InlineData(" key ", 15000)]
    [InlineData("key", 999)]
    [InlineData("key", 60001)]
    public void Enabled_configuration_rejects_invalid_keys_or_timeout(string key, int timeout)
    {
        using var provider = Services(new() { ["PayOSPayout:Enabled"] = "true", ["PayOSPayout:ClientId"] = key,
            ["PayOSPayout:ApiKey"] = key, ["PayOSPayout:ChecksumKey"] = key,
            ["PayOSPayout:TimeoutMilliseconds"] = timeout.ToString() }).BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<PayOSPayoutSettings>>().Value);
    }

    [Fact]
    public void Payout_client_does_not_replace_collection_client_or_make_network_calls_on_resolution()
    {
        var collection = new PayOSClient("collection-client", "collection-api", "collection-checksum");
        var services = Services(new() { ["PayOSPayout:Enabled"] = "true", ["PayOSPayout:ClientId"] = "payout-client",
            ["PayOSPayout:ApiKey"] = "payout-api", ["PayOSPayout:ChecksumKey"] = "payout-checksum" });
        services.AddSingleton(collection);
        using var provider = services.BuildServiceProvider();
        Assert.Same(collection, provider.GetRequiredService<PayOSClient>());
        Assert.NotNull(provider.GetRequiredService<PayOSPayoutClient>());
        Assert.IsType<PayOSPayoutGateway>(provider.GetRequiredService<IPayOSPayoutGateway>());
    }

    [Fact]
    public async Task Invalid_enabled_configuration_fails_host_startup()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["PayOSPayout:Enabled"] = "true" });
        builder.Services.AddPayOSPayoutConfiguration(builder.Configuration);
        using var host = builder.Build();
        await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
    }

    [Fact]
    public async Task Disabled_gateway_rejects_all_operations()
    {
        var gateway = new DisabledPayOSPayoutGateway();
        await Assert.ThrowsAsync<PayOSPayoutDisabledException>(() => gateway.GetBalanceAsync(default));
        await Assert.ThrowsAsync<PayOSPayoutDisabledException>(() => gateway.EstimateAsync(Valid, default));
        await Assert.ThrowsAsync<PayOSPayoutDisabledException>(() => gateway.PreflightAsync(Valid, default));
    }

    [Fact]
    public async Task Negative_provider_credit_is_rejected()
    {
        using var merchant = new Merchant { EstimatedCredit = -1 };
        var error = await Assert.ThrowsAsync<PayOSPayoutGatewayException>(() => Gateway(merchant).EstimateAsync(Valid, default));
        Assert.Equal("PAYOUT_ESTIMATE_INVALID", error.Code);
    }

    [Fact]
    public async Task Caller_cancellation_is_preserved()
    {
        using var merchant = new Merchant { Cancel = true };
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Gateway(merchant).GetBalanceAsync(cancellation.Token));
    }

    [Fact]
    public async Task Provider_timeout_is_sanitized_and_retryable()
    {
        using var merchant = new Merchant { Cancel = true };
        var error = await Assert.ThrowsAsync<PayOSPayoutGatewayException>(() => Gateway(merchant).GetBalanceAsync(default));
        Assert.Equal("PAYOUT_LOOKUP_UNAVAILABLE", error.Code);
        Assert.True(error.Retryable);
        Assert.DoesNotContain("provider-private", error.ToString());
        Assert.Equal(1, merchant.Calls);
    }

    [Fact]
    public async Task Cancellation_during_sdk_request_is_preserved_even_if_sdk_wraps_it()
    {
        using var cancellation = new CancellationTokenSource();
        using var merchant = new Merchant { Cancel = true, OnRequest = cancellation.Cancel };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Gateway(merchant).GetBalanceAsync(cancellation.Token));
        Assert.Equal(1, merchant.Calls);
    }

    [Fact]
    public async Task Preflight_uses_real_sdk_read_only_routes_and_keeps_credit_separate_from_principal()
    {
        using var merchant = new Merchant();
        var result = await Gateway(merchant).PreflightAsync(Valid, default);
        Assert.Equal(100000m, result.Balance.Balance);
        Assert.Equal(5000, result.Cost.EstimatedCredit);
        Assert.True(result.HasSufficientPrincipalBalance);
        Assert.True(result.Cost.DestinationValidationRequested);
        Assert.False(result.AccountNameVerified);
        Assert.Null(result.RecipientAccountName);
        Assert.Equal(2, merchant.Calls);
    }

    [Theory]
    [InlineData("0", "VND", true)]
    [InlineData("-1", "VND", false)]
    [InlineData("100,000", "VND", false)]
    [InlineData("invalid", "VND", false)]
    [InlineData("100000", "USD", false)]
    public async Task Balance_rejects_invalid_provider_values(string balance, string currency, bool valid)
    {
        using var merchant = new Merchant { Balance = balance, Currency = currency };
        if (valid) Assert.Equal(0, (await Gateway(merchant).GetBalanceAsync(default)).Balance);
        else Assert.Equal("PAYOUT_BALANCE_INVALID", (await Assert.ThrowsAsync<PayOSPayoutGatewayException>(
            () => Gateway(merchant).GetBalanceAsync(default))).Code);
    }

    [Fact]
    public async Task Insufficient_balance_and_disabled_destination_validation_are_explicit()
    {
        using var merchant = new Merchant { Balance = "99999", ValidateDestination = false };
        var result = await Gateway(merchant, false).PreflightAsync(Valid, default);
        Assert.False(result.HasSufficientPrincipalBalance);
        Assert.False(result.Cost.DestinationValidationRequested);
        Assert.False(result.AccountNameVerified);
    }

    [Fact]
    public async Task Invalid_inputs_never_reach_provider()
    {
        using var merchant = new Merchant();
        var gateway = Gateway(merchant);
        foreach (var request in new[] { Valid with { Amount = 0 }, Valid with { Amount = 1.5m },
            Valid with { Amount = 9007199254740992m }, Valid with { RecipientBankBin = "abc123" },
            Valid with { RecipientAccountNumber = "12 34" }, Valid with { ReferenceId = "bad/reference" },
            Valid with { Description = "Hoan\ncoc" } })
            await Assert.ThrowsAsync<PayOSPayoutValidationException>(() => gateway.PreflightAsync(request, default));
        Assert.Equal(0, merchant.Calls);
    }

    [Theory]
    [InlineData(401, "PAYOUT_ACCESS_DENIED")]
    [InlineData(403, "PAYOUT_ACCESS_DENIED")]
    [InlineData(400, "PAYOUT_PREFLIGHT_REJECTED")]
    [InlineData(503, "PAYOUT_LOOKUP_UNAVAILABLE")]
    public async Task Provider_errors_are_sanitized_and_not_retried(int status, string code)
    {
        using var merchant = new Merchant { Status = (HttpStatusCode)status };
        var error = await Assert.ThrowsAsync<PayOSPayoutGatewayException>(() => Gateway(merchant).GetBalanceAsync(default));
        Assert.Equal(code, error.Code);
        Assert.DoesNotContain("provider-private", error.ToString());
        Assert.Equal(1, merchant.Calls);
    }

    private static ServiceCollection Services(Dictionary<string, string?> values)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddPayOSPayoutConfiguration(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        return services;
    }

    private static PayOSPayoutGateway Gateway(Merchant merchant, bool validate = true) => new(
        new PayOSPayoutClient(new PayOSClient(new PayOSOptions { ClientId = "payout-client", ApiKey = "payout-api",
            ChecksumKey = "payout-checksum", BaseUrl = "https://payout.test.invalid", MaxRetries = 0,
            HttpClient = new HttpClient(merchant, false) })),
        Options.Create(new PayOSPayoutSettings { Enabled = true, ValidateDestinationOnEstimate = validate }), TimeProvider.System);

    private sealed class Merchant : HttpMessageHandler
    {
        public int Calls;
        public string Balance = "100000";
        public string Currency = "VND";
        public bool ValidateDestination = true;
        public bool Cancel;
        public Action? OnRequest;
        public long EstimatedCredit = 5000;
        public HttpStatusCode Status = HttpStatusCode.OK;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Calls);
            OnRequest?.Invoke();
            if (Cancel) throw new OperationCanceledException("provider-private", cancellationToken);
            Assert.Equal("payout.test.invalid", request.RequestUri!.Host);
            Assert.Equal("payout-client", request.Headers.GetValues("x-client-id").Single());
            Assert.Equal("payout-api", request.Headers.GetValues("x-api-key").Single());
            if (Status != HttpStatusCode.OK) return new(Status) { Content = JsonContent.Create(new { code = "error", desc = "provider-private" }) };
            object data;
            if (request.Method == HttpMethod.Get)
            {
                Assert.Equal("/v1/payouts-account/balance", request.RequestUri.AbsolutePath);
                data = new { accountNumber = "123", accountName = "ADMIN", currency = Currency, balance = Balance };
            }
            else
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("/v1/payouts/estimate-credit", request.RequestUri.AbsolutePath);
                Assert.True(request.Headers.Contains("x-signature"));
                var body = await request.Content!.ReadFromJsonAsync<JsonElement>(cancellationToken);
                var signer = new PayOSClient("payout-client", "payout-api", "payout-checksum");
                Assert.Equal(signer.Crypto.CreateSignature("payout-checksum", body, new PayOS.Crypto.CryptoSignatureOptions()),
                    request.Headers.GetValues("x-signature").Single());
                Assert.Equal(ValidateDestination, body.GetProperty("validateDestination").GetBoolean());
                var item = Assert.Single(body.GetProperty("payouts").EnumerateArray());
                Assert.Equal("00123456", item.GetProperty("toAccountNumber").GetString());
                Assert.Equal(100000, item.GetProperty("amount").GetInt64());
                Assert.Equal("970422", item.GetProperty("toBin").GetString());
                Assert.Equal("refund-1", item.GetProperty("referenceId").GetString());
                data = new { estimateCredit = EstimatedCredit };
            }
            return new(Status) { Content = JsonContent.Create(new { code = "00", desc = "success", data }) };
        }
    }
}
