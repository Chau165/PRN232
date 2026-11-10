using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PayOS;
using PRN232.Api.Configuration;
using PRN232.Application.Settings;
using Xunit;

namespace PRN232.Application.Tests;

public class PayOSConfigurationTests
{
    private static Dictionary<string, string?> Valid() => new()
    {
        ["PayOS:Enabled"] = "true",
        ["PayOS:ClientId"] = "test-client-id",
        ["PayOS:ApiKey"] = "test-api-key",
        ["PayOS:ChecksumKey"] = "test-checksum-key",
        ["PayOS:ReturnUrl"] = "https://frontend.example.test/my-bills?payment=return",
        ["PayOS:CancelUrl"] = "https://frontend.example.test/my-bills?payment=cancel"
    };

    private static IHost Host(Dictionary<string, string?> values, string environment = "Production") =>
        new HostBuilder().UseEnvironment(environment)
            .ConfigureAppConfiguration(builder => builder.AddInMemoryCollection(values))
            .ConfigureServices((context, services) => services.AddPayOSConfiguration(context.Configuration, context.HostingEnvironment))
            .Build();

    [Fact]
    public async Task Disabled_configuration_starts_without_credentials_or_client()
    {
        using var host = Host(new Dictionary<string, string?>());
        await host.StartAsync();
        Assert.False(host.Services.GetRequiredService<IOptions<PayOSSettings>>().Value.Enabled);
        Assert.Null(host.Services.GetService<PayOSClient>());
        await host.StopAsync();
    }

    [Fact]
    public async Task Valid_configuration_registers_sdk_without_network_calls()
    {
        using var host = Host(Valid());
        await host.StartAsync();
        Assert.NotNull(host.Services.GetRequiredService<PayOSClient>());
        var settings = host.Services.GetRequiredService<IOptions<PayOSSettings>>().Value;
        Assert.Equal("https://frontend.example.test/my-bills?payment=return", settings.ReturnUrl);
        Assert.Same(host.Services.GetRequiredService<PayOSClient>(), host.Services.GetRequiredService<PayOSClient>());
        await host.StopAsync();
    }

    [Theory]
    [InlineData("ClientId")]
    [InlineData("ApiKey")]
    [InlineData("ChecksumKey")]
    public async Task Missing_credentials_fail_at_startup_without_echoing_secrets(string key)
    {
        var values = Valid();
        values["PayOS:" + key] = "";
        using var host = Host(values);
        var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
        Assert.Contains("PayOS:" + key, error.Message);
        Assert.DoesNotContain("test-api-key", error.Message);
        Assert.DoesNotContain("test-checksum-key", error.Message);
    }

    [Theory]
    [InlineData("ReturnUrl", "/my-bills", "Production", false)]
    [InlineData("CancelUrl", "ftp://example.test/cancel", "Production", false)]
    [InlineData("ReturnUrl", "https://user:password@example.test/return", "Production", false)]
    [InlineData("CancelUrl", "https://example.test/cancel#fragment", "Production", false)]
    [InlineData("ReturnUrl", "http://example.test/return", "Development", false)]
    [InlineData("ReturnUrl", "http://localhost:8443/my-bills", "Production", false)]
    [InlineData("ReturnUrl", "http://localhost:8443/my-bills", "Development", true)]
    [InlineData("CancelUrl", "http://127.0.0.1:8443/my-bills", "Testing", true)]
    [InlineData("ReturnUrl", "", "Production", false)]
    public async Task Callback_validation_respects_environment(string key, string value, string environment, bool valid)
    {
        var values = Valid();
        values["PayOS:" + key] = value;
        using var host = Host(values, environment);
        if (valid)
        {
            await host.StartAsync();
            await host.StopAsync();
        }
        else
        {
            var error = await Assert.ThrowsAsync<OptionsValidationException>(() => host.StartAsync());
            Assert.Contains("PayOS:" + key, error.Message);
            Assert.DoesNotContain("user:password", error.Message);
        }
    }
}
