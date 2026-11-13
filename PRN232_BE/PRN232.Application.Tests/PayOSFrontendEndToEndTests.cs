using System.Diagnostics;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PayOS;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.PaymentGateways;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class PayOSFrontendFactAttribute : TheoryAttribute
{
    public PayOSFrontendFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("RUN_PAYOS_FE_E2E") != "1")
            Skip = "Opt-in cross-process FE/BE test: set RUN_PAYOS_FE_E2E=1; requires installed FE node_modules.";
    }
}

public class PayOSFrontendEndToEndTests
{
    [PayOSFrontendFact]
    [InlineData("webhook")]
    [InlineData("recovery")]
    [InlineData("cash")]
    public async Task Frontend_sdk_webhook_database_and_reports_agree(string scenario)
    {
        using var merchant = new PayOSTestMerchant { LoseCreateResponse = scenario == "recovery" };
        using var factory = new BookingsApiFactory();
        using var app = factory.WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, config) =>
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["PayOS:Enabled"] = "true", ["PayOS:ClientId"] = "test-client", ["PayOS:ApiKey"] = "test-api",
                ["PayOS:ChecksumKey"] = PayOSTestMerchant.Key, ["PayOS:ReturnUrl"] = "https://frontend.test/payment-result",
                ["PayOS:CancelUrl"] = "https://frontend.test/payment-result", ["PayOS:ReconciliationEnabled"] = "false",
                ["Logging:LogLevel:Default"] = "None"
            })).ConfigureTestServices(services =>
            {
                services.RemoveAll<PayOSClient>();
                services.AddSingleton(new PayOSClient(new PayOSOptions { ClientId = "test-client", ApiKey = "test-api",
                    ChecksumKey = PayOSTestMerchant.Key, BaseUrl = "https://payos.test.invalid", HttpClient = new HttpClient(merchant), MaxRetries = 0 }));
                services.RemoveAll<IInvoiceCheckoutGateway>();
                services.RemoveAll<IPayOSWebhookVerifier>();
                services.RemoveAll<IPayOSReconciliationGateway>();
                services.RemoveAll<IPayOSCancellationGateway>();
                services.AddScoped<IInvoiceCheckoutGateway>(provider => new DiagnosticCheckoutGateway(
                    ActivatorUtilities.CreateInstance<PayOSInvoiceCheckoutGateway>(provider), merchant));
                services.AddScoped<IPayOSWebhookVerifier, PayOSWebhookVerifier>();
                services.AddScoped<IPayOSReconciliationGateway, PayOSReconciliationGateway>();
                services.AddScoped<IPayOSCancellationGateway, PayOSCancellationGateway>();
            }));
        using var backend = app.CreateClient();
        var invoiceId = await InvoicesApiTests.CreateInvoiceAsync(backend);
        var managerToken = backend.DefaultRequestHeaders.Authorization!.Parameter!;
        await BookingsApiTests.LoginAsync(backend, "customer@example.test");
        var customerToken = backend.DefaultRequestHeaders.Authorization!.Parameter!;
        backend.DefaultRequestHeaders.Authorization = null;

        // Loopback-only bridge and controls live exclusively in this test assembly, never Program.cs.
        var bridgeBuilder = WebApplication.CreateBuilder();
        bridgeBuilder.Logging.ClearProviders();
        bridgeBuilder.WebHost.UseUrls("http://127.0.0.1:0");
        await using var bridge = bridgeBuilder.Build();
        bridge.MapPost("/__test/pay/{action}", async (string action) =>
        {
            if (action is "complete" or "paid-without-webhook" or "extra") merchant.Pay();
            if (action == "paid-without-webhook") return Results.Ok();
            var response = await backend.PostAsJsonAsync("/api/payments/payos/webhook", merchant.Webhook(action == "tamper", action == "extra"));
            return Results.StatusCode((int)response.StatusCode);
        });
        bridge.MapPost("/__test/due", async () =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await db.PaymentRequests.SingleAsync();
            request.CreatedAtUtc = DateTime.UtcNow.AddMinutes(-5);
            request.UpdatedAtUtc = DateTime.UtcNow.AddMinutes(-2);
            await db.SaveChangesAsync();
            return Results.Ok();
        });
        bridge.MapGet("/__test/db", async () =>
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var request = await db.PaymentRequests.SingleAsync();
            var payment = await db.Payments.AsNoTracking().SingleOrDefaultAsync(item => item.InvoiceId == invoiceId);
            return Results.Json(new { invoiceStatus = (await db.Invoices.SingleAsync()).Status,
                requestId = request.PaymentRequestId, requestStatus = request.Status, request.PaymentId,
                paymentMethod = payment?.PaymentMethod, paidAtUtc = payment?.PaidAtUtc, amount = payment?.Amount,
                paymentCount = await db.Payments.CountAsync(item => item.InvoiceId == invoiceId),
                receiptCount = await db.PaymentWebhookReceipts.CountAsync(), reviewCount = await db.PaymentReviewCases.CountAsync(),
                merchant.Creates, merchant.Gets, merchant.Cancels, merchant.CheckoutFailure });
        });
        bridge.MapMethods("/api/{**path}", ["GET", "POST"], async (HttpContext context) =>
        {
            using var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), context.Request.Path + context.Request.QueryString);
            if (context.Request.Headers.TryGetValue("Authorization", out var authorization)) request.Headers.TryAddWithoutValidation("Authorization", authorization.ToArray());
            if (context.Request.ContentLength > 0)
            {
                using var reader = new StreamReader(context.Request.Body);
                request.Content = new StringContent(await reader.ReadToEndAsync(), System.Text.Encoding.UTF8, "application/json");
            }
            using var response = await backend.SendAsync(request);
            context.Response.StatusCode = (int)response.StatusCode;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(await response.Content.ReadAsStringAsync());
        });
        await bridge.StartAsync();
        var origin = bridge.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.Single();
        var root = FindRoot();
        var start = new ProcessStartInfo("node") { WorkingDirectory = Path.Combine(root, "PRN232_FE"),
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8, StandardErrorEncoding = System.Text.Encoding.UTF8 };
        foreach (var arg in new[] { "node_modules/vitest/vitest.mjs", "run", "--config", "vitest.payos-e2e.config.ts", "--maxWorkers=1" }) start.ArgumentList.Add(arg);
        start.Environment["VITE_API_BASE_URL"] = origin + "/api";
        start.Environment["PAYOS_E2E_ORIGIN"] = origin;
        start.Environment["PAYOS_E2E_SCENARIO"] = scenario;
        start.Environment["PAYOS_E2E_CUSTOMER_TOKEN"] = customerToken;
        start.Environment["PAYOS_E2E_MANAGER_TOKEN"] = managerToken;
        start.Environment["PAYOS_E2E_INVOICE_ID"] = invoiceId.ToString();
        using var process = Process.Start(start)!;
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(100));
        try { await process.WaitForExitAsync(timeout.Token); }
        catch { process.Kill(true); throw; }
        Assert.True(process.ExitCode == 0, await stdout + "\n" + await stderr);
        await bridge.StopAsync();
    }

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "PRN232_FE"))) directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Cannot locate the FE workspace.");
    }

    private sealed class DiagnosticCheckoutGateway(IInvoiceCheckoutGateway inner, PayOSTestMerchant merchant) : IInvoiceCheckoutGateway
    {
        public async Task<CheckoutLink> CreateAsync(long orderCode, decimal amount, DateTime expiresUtc, CancellationToken cancellationToken)
        {
            try { return await inner.CreateAsync(orderCode, amount, expiresUtc, cancellationToken); }
            catch (Exception exception) { merchant.CheckoutFailure = exception.ToString(); throw; }
        }
        public Task<CheckoutStatus> GetStatusAsync(long orderCode, CancellationToken cancellationToken) => inner.GetStatusAsync(orderCode, cancellationToken);
    }
}
