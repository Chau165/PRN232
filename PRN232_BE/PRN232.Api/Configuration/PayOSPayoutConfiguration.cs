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
            .Validate(value => value.ReconciliationIntervalSeconds is >= 5 and <= 3600, "PayOSPayout:ReconciliationIntervalSeconds must be 5..3600.")
            .Validate(value => value.ReconciliationBatchSize is >= 1 and <= 100, "PayOSPayout:ReconciliationBatchSize must be 1..100.")
            .Validate(value => value.ReconciliationMinimumAgeSeconds is >= 30 and <= 86400, "PayOSPayout:ReconciliationMinimumAgeSeconds must be 30..86400.")
            .Validate(value => !value.ReconciliationEnabled || value.ReconciliationMinimumAgeSeconds >= 2 * value.TimeoutMilliseconds / 1000 + 30,
                "PayOSPayout:ReconciliationMinimumAgeSeconds must allow both preflight and submission timeouts plus 30 seconds.")
            .Validate(value => value.ReconciliationStuckAfterSeconds >= value.ReconciliationMinimumAgeSeconds && value.ReconciliationStuckAfterSeconds <= 604800,
                "PayOSPayout:ReconciliationStuckAfterSeconds must be between minimum age and 604800.")
            .Validate(value => value.MaxPayoutAttempts is >= 1 and <= 5 && value.PayoutRetryDelaySeconds is >= 60 and <= 86400, "PayOSPayout retry limits are invalid.")
            .Validate(value => !value.ReconciliationEnabled || value.Enabled, "PayOSPayout:Enabled is required for reconciliation.")
            .Validate(value => !value.RetryFailedPayouts || value.ReconciliationEnabled, "PayOSPayout:ReconciliationEnabled is required for retries.")
            .Validate(value => value.DispatchIntervalSeconds is >= 5 and <= 3600, "PayOSPayout:DispatchIntervalSeconds must be 5..3600.")
            .Validate(value => value.DispatchBatchSize is >= 1 and <= 100, "PayOSPayout:DispatchBatchSize must be 1..100.")
            .Validate(value => !value.DispatchEnabled || value.Enabled, "PayOSPayout:Enabled is required for dispatch.")
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
            services.AddScoped<IPayOSPayoutSubmissionGateway, PayOSPayoutSubmissionGateway>();
            services.AddScoped<IPayOSPayoutReconciliationGateway, PayOSPayoutReconciliationGateway>();
        }
        else
        {
            services.AddScoped<IPayOSPayoutGateway, DisabledPayOSPayoutGateway>();
            services.AddScoped<IPayOSPayoutSubmissionGateway, DisabledPayOSPayoutSubmissionGateway>();
            services.AddScoped<IPayOSPayoutReconciliationGateway, DisabledPayOSPayoutReconciliationGateway>();
        }
        return services;
    }

    private static bool Credential(string? value) => !string.IsNullOrWhiteSpace(value) && value == value.Trim() &&
        !value.StartsWith('<') && !value.StartsWith("your-", StringComparison.OrdinalIgnoreCase) &&
        !value.StartsWith("replace-", StringComparison.OrdinalIgnoreCase);
}
