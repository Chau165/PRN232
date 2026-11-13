using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Api.Controllers;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ApiSecurityTests
{
    // A1: hạn mức login tính riêng từng IP (đọc từ X-Forwarded-For như khi chạy sau proxy Railway).
    [Fact]
    public async Task Login_rate_limit_is_per_client_ip_and_returns_429()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var allowed = await LoginFromAsync(client, "203.0.113.1");
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        }
        using var blocked = await LoginFromAsync(client, "203.0.113.1");
        using var otherClient = await LoginFromAsync(client, "203.0.113.2");

        Assert.Equal(HttpStatusCode.TooManyRequests, blocked.StatusCode);
        Assert.Equal(HttpStatusCode.OK, otherClient.StatusCode);
    }

    // A4: khoá tài khoản thì token đã cấp trước đó cũng hết dùng được ngay.
    [Fact]
    public async Task Token_of_deactivated_user_is_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/bookings")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.SingleAsync(item => item.Email == "customer@example.test");
            user.IsActive = false;
            await context.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/bookings")).StatusCode);
    }

    [Fact]
    public async Task Token_of_deactivated_user_is_rejected_by_notification_hub()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var token = client.DefaultRequestHeaders.Authorization!.Parameter!;
        client.DefaultRequestHeaders.Authorization = null;

        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.SingleAsync(item => item.Email == "customer@example.test");
            user.IsActive = false;
            await context.SaveChangesAsync();
        }

        using var response = await client.PostAsync(
            $"/hubs/notifications/negotiate?negotiateVersion=1&access_token={Uri.EscapeDataString(token)}", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // A2: Production mà còn dùng key mẫu trên GitHub thì không cho server khởi động.
    [Theory]
    [InlineData("PUT_A_REAL_SECRET_HERE_VIA_USER_SECRETS_DO_NOT_COMMIT")]
    [InlineData("too-short")]
    public void Production_refuses_to_start_with_placeholder_or_short_jwt_key(string key)
    {
        using var factory = new ProductionFactory(key);

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("Jwt:Key", exception.ToString());
    }

    // A9: thiếu khóa HMAC của CCCD thì kiểm tra trùng CCCD sai, nên Production không được khởi động.
    [Theory]
    [InlineData(null)]
    [InlineData("too-short")]
    public void Production_refuses_to_start_without_a_cccd_hash_key(string? cccdHashKey)
    {
        // UseSetting (not ConfigureAppConfiguration) so Program.cs sees the values before Build().
        using var factory = new WebApplicationFactory<BookingsController>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("Jwt:Key", "a-real-looking-production-jwt-signing-key-0123456789");
            builder.UseSetting("IdentityDocuments:CccdHashKey", cccdHashKey);
            builder.UseSetting("ConnectionStrings:DefaultConnection", "Host=localhost;Database=unused");
        });

        var exception = Assert.ThrowsAny<Exception>(() => factory.CreateClient());

        Assert.Contains("IdentityDocuments:CccdHashKey", exception.ToString());
        Assert.DoesNotContain("Jwt:Key chưa được cấu hình", exception.ToString());
    }

    private sealed class ProductionFactory(string jwtKey) : WebApplicationFactory<BookingsController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Production");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Key"] = jwtKey,
                ["ConnectionStrings:DefaultConnection"] = "Host=localhost;Database=unused"
            }));
        }
    }

    private static Task<HttpResponseMessage> LoginFromAsync(HttpClient client, string ip)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new
            {
                email = "customer@example.test",
                password = BookingsApiTests.Password
            })
        };
        request.Headers.Add("X-Forwarded-For", ip);
        return client.SendAsync(request);
    }
}
