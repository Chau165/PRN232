using Microsoft.Extensions.Options;
using PayOS;
using PRN232.Application.Settings;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.PaymentGateways;

namespace PRN232.Api.Configuration;

public static class PayOSConfiguration
{
    public static IServiceCollection AddPayOSConfiguration(
        this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        var section = configuration.GetSection(PayOSSettings.SectionName);
        services.AddOptions<PayOSSettings>().Bind(section)
            .Validate(settings => !settings.Enabled || HasCredential(settings.ClientId), "PayOS:ClientId is required when enabled.")
            .Validate(settings => !settings.Enabled || HasCredential(settings.ApiKey), "PayOS:ApiKey is required when enabled.")
            .Validate(settings => !settings.Enabled || HasCredential(settings.ChecksumKey), "PayOS:ChecksumKey is required when enabled.")
            .Validate(settings => !settings.Enabled || ValidCallback(settings.ReturnUrl, environment),
                "PayOS:ReturnUrl must be an absolute HTTPS URL (HTTP loopback allowed only in Development/Testing), without credentials or fragment.")
            .Validate(settings => !settings.Enabled || ValidCallback(settings.CancelUrl, environment),
                "PayOS:CancelUrl must be an absolute HTTPS URL (HTTP loopback allowed only in Development/Testing), without credentials or fragment.")
            .ValidateOnStart();

        if (section.GetValue<bool>(nameof(PayOSSettings.Enabled)))
        {
            services.AddScoped<IInvoiceCheckoutGateway, PayOSInvoiceCheckoutGateway>();
            services.AddScoped<IPayOSWebhookVerifier, PayOSWebhookVerifier>();
            services.AddSingleton(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<PayOSSettings>>().Value;
                // Explicit credentials avoid silently using unrelated SDK environment defaults.
                // Construction performs no payment call or webhook registration.
                return new PayOSClient(settings.ClientId, settings.ApiKey, settings.ChecksumKey);
            });
        }
        else
        {
            services.AddScoped<IInvoiceCheckoutGateway, DisabledInvoiceCheckoutGateway>();
            services.AddScoped<IPayOSWebhookVerifier, DisabledPayOSWebhookVerifier>();
        }
        return services;
    }

    private static bool HasCredential(string? value) => !string.IsNullOrWhiteSpace(value) &&
        value == value.Trim() && !value.StartsWith('<') &&
        !value.StartsWith("your-", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);

    private static bool ValidCallback(string? value, IHostEnvironment environment)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment)) return false;
        return uri.Scheme == Uri.UriSchemeHttps ||
            (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback &&
                (environment.IsDevelopment() || environment.IsEnvironment("Testing")));
    }
}
