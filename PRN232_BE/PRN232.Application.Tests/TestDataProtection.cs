using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace PRN232.Application.Tests;

/// <summary>
/// EF Core caches the compiled AppDbContext model, including the CCCD value converter and the
/// protector it captured (A9), for the whole process. The app has a single host, but tests create
/// and dispose many hosts in one process, so a protector captured from a disposed test host would
/// fail later tests. Every test host therefore shares this one long-lived provider.
/// </summary>
internal static class TestDataProtection
{
    public static IDataProtectionProvider Provider { get; } = new EphemeralDataProtectionProvider();

    public static IServiceCollection UseSharedTestDataProtection(this IServiceCollection services)
    {
        services.RemoveAll<IDataProtectionProvider>();
        services.AddSingleton(Provider);
        return services;
    }
}
