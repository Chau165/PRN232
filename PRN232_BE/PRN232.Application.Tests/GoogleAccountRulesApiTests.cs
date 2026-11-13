using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// Rules for accounts created through Google sign-in: no password yet and no phone number.
public sealed class GoogleAccountRulesApiTests
{
    private const string GoogleToken = "token-google-customer";
    private const string GoogleEmail = "google.customer@example.test";
    private static readonly DateTime FutureUtc = DateTime.UtcNow.Date.AddDays(2).AddHours(9);

    [Fact]
    public async Task Google_user_reports_missing_password_and_phone_until_both_are_added()
    {
        using var host = new Host();
        var auth = await host.SignInWithGoogleAsync();

        Assert.False(auth.User.HasPassword);
        Assert.True(auth.User.PhoneNumberMissing);
        var me = await host.Client.GetFromJsonAsync<UserDto>("/api/users/me");
        Assert.False(me!.HasPassword);
        Assert.True(me.PhoneNumberMissing);

        using var setPassword = await host.Client.PutAsJsonAsync("/api/users/me/password", new
        {
            newPassword = "NewPassword123"
        });
        Assert.Equal(HttpStatusCode.NoContent, setPassword.StatusCode);
        using var updateProfile = await host.Client.PutAsJsonAsync("/api/users/me", new
        {
            fullName = "Google Customer",
            phoneNumber = "0915000000"
        });
        Assert.True(updateProfile.IsSuccessStatusCode);

        me = await host.Client.GetFromJsonAsync<UserDto>("/api/users/me");
        Assert.True(me!.HasPassword);
        Assert.False(me.PhoneNumberMissing);

        // The new password now works for normal login.
        using var login = await host.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = GoogleEmail,
            password = "NewPassword123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var loginAuth = await login.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.True(loginAuth!.User.HasPassword);
        Assert.False(loginAuth.User.PhoneNumberMissing);
    }

    [Fact]
    public async Task Setting_a_first_password_still_applies_the_password_rules()
    {
        using var host = new Host();
        await host.SignInWithGoogleAsync();

        using var weak = await host.Client.PutAsJsonAsync("/api/users/me/password", new { newPassword = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await context.Users.SingleAsync(user => user.Email == GoogleEmail)).PasswordHash);
    }

    [Fact]
    public async Task Accounts_with_a_password_still_need_the_current_password()
    {
        using var host = new Host();
        await BookingsApiTests.LoginAsync(host.Client, "customer@example.test");

        using var withoutCurrent = await host.Client.PutAsJsonAsync("/api/users/me/password", new
        {
            newPassword = "AnotherPassword123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, withoutCurrent.StatusCode);
        using var body = JsonDocument.Parse(await withoutCurrent.Content.ReadAsStringAsync());
        Assert.Equal("Invalid current password", body.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Booking_and_viewing_requests_need_a_phone_number()
    {
        using var host = new Host();
        await host.SignInWithGoogleAsync();

        using var booking = await host.Client.PostAsJsonAsync("/api/bookings", new { roomId = 1 });
        using var viewing = await host.Client.PostAsJsonAsync("/api/viewing-schedules", new
        {
            roomId = 1,
            scheduledAt = FutureUtc
        });

        await AssertPhoneRequiredAsync(booking, PhoneNumberRequiredException.SelfMessage);
        await AssertPhoneRequiredAsync(viewing, PhoneNumberRequiredException.SelfMessage);
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.Bookings.AnyAsync());
        Assert.False(await context.ViewingSchedules.AnyAsync());

        // After adding a phone number the same requests go through.
        using var update = await host.Client.PutAsJsonAsync("/api/users/me", new
        {
            fullName = "Google Customer",
            phoneNumber = "0915000000"
        });
        Assert.True(update.IsSuccessStatusCode);
        using var retry = await host.Client.PostAsJsonAsync("/api/viewing-schedules", new
        {
            roomId = 1,
            scheduledAt = FutureUtc
        });
        Assert.Equal(HttpStatusCode.Created, retry.StatusCode);
    }

    [Fact]
    public async Task Contract_for_a_customer_without_phone_number_is_refused()
    {
        using var host = new Host();
        var bookingId = await ContractsApiTests.CreatePaidBookingAsync(host.Client);
        using (var scope = host.Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var customer = await context.Users.SingleAsync(user => user.UserId == 10);
            customer.PhoneNumber = "";
            await context.SaveChangesAsync();
        }

        await BookingsApiTests.LoginAsync(host.Client, "manager@example.test");
        var start = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(2);
        using var response = await ContractsApiTests.PostContractAsync(host.Client, bookingId, start, start.AddYears(1));

        await AssertPhoneRequiredAsync(response, PhoneNumberRequiredException.CustomerMessage);
        using var verify = host.Factory.Services.CreateScope();
        Assert.False(await verify.ServiceProvider.GetRequiredService<AppDbContext>().Contracts.AnyAsync());
    }

    private static async Task AssertPhoneRequiredAsync(HttpResponseMessage response, string message)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(message, body.RootElement.GetProperty("detail").GetString());
        Assert.Contains("Vui lòng cập nhật số điện thoại trong trang cá nhân.", message);
    }

    private sealed class Host : IDisposable
    {
        private readonly BookingsApiFactory _baseFactory = new();

        public Host()
        {
            Factory = _baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleIdTokenValidator>();
                services.AddSingleton<IGoogleIdTokenValidator>(new FakeValidator());
            }));
            Client = Factory.CreateClient();
        }

        public WebApplicationFactory<PRN232.Api.Controllers.BookingsController> Factory { get; }
        public HttpClient Client { get; }

        public async Task<AuthResponseDto> SignInWithGoogleAsync()
        {
            using var response = await Client.PostAsJsonAsync("/api/auth/google", new { idToken = GoogleToken });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var auth = (await response.Content.ReadFromJsonAsync<AuthResponseDto>())!;
            Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
            return auth;
        }

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
            _baseFactory.Dispose();
        }
    }

    private sealed class FakeValidator : IGoogleIdTokenValidator
    {
        public bool IsConfigured => true;

        public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken) =>
            Task.FromResult<GoogleIdentity?>(idToken == GoogleToken
                ? new GoogleIdentity("sub-google-customer", GoogleEmail, true, "Google Customer")
                : null);
    }
}
