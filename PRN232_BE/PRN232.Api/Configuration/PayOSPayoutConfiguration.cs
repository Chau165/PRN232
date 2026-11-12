using Microsoft.Extensions.Options;
using PayOS;
using PRN232.Application.Interfaces;
using PRN232.Application.Settings;
using PRN232.Infrastructure.PaymentGateways;

namespace PRN232.Api.Configuration;

public static class PayOSPayoutConfiguration
{
    public static IServiceCollection AddPayOSPayoutConfiguration(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(PayOSPayoutSettings.SectionName);
        services.AddOptions<PayOSPayoutSettings>().Bind(section)
            .Validate(value => value.TimeoutMilliseconds is >= 1000 and <= 60000, "PayOSPayout:TimeoutMilliseconds must be 1000..60000.")
            .Validate(value => !value.Enabled || Credential(value.ClientId), "PayOSPayout:ClientId is required when enabled.")
            .Validate(value => !value.Enabled || Credential(value.ApiKey), "PayOSPayout:ApiKey is required when enabled.")
            .Validate(value => !value.Enabled || Credential(value.ChecksumKey), "PayOSPayout:ChecksumKey is required when enabled.")
            .ValidateOnStart();
        if (section.GetValue<bool>(nameof(PayOSPayoutSettings.Enabled)))
        {
            services.AddSingleton(provider =>
            {
                var settings = provider.GetRequiredService<IOptions<PayOSPayoutSettings>>().Value;
                return new PayOSPayoutClient(new PayOSClient(new PayOSOptions
                {
                    ClientId = settings.ClientId, ApiKey = settings.ApiKey, ChecksumKey = settings.ChecksumKey,
                    TimeoutMs = settings.TimeoutMilliseconds, MaxRetries = 0,
                    Logger = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance,
                    LogLevel = LogLevel.None
                }));
            });
            services.AddScoped<IPayOSPayoutGateway, PayOSPayoutGateway>();
        }
        else services.AddScoped<IPayOSPayoutGateway, DisabledPayOSPayoutGateway>();
        return services;
    }

    private static bool Credential(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim() &&
        !value.StartsWith('<') && !value.StartsWith("your-", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);
}
