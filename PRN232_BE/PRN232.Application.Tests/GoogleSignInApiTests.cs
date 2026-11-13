using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// POST /api/auth/google: verify the Google ID token (faked here), then issue our own JWT.
public sealed class GoogleSignInApiTests
{
    private const string NewUserToken = "token-new-user";
    private const string ExistingEmailToken = "token-existing-email";
    private const string UnverifiedToken = "token-unverified";
    private const string InactiveToken = "token-inactive";
    private const string OtherSubjectToken = "token-other-subject";
    private const string NoNameToken = "token-no-name";

    private static readonly Dictionary<string, GoogleIdentity> Identities = new()
    {
        [NewUserToken] = new("sub-new", "New.Person@Gmail.test", true, "Nguyễn Văn Mới"),
        [ExistingEmailToken] = new("sub-existing", "customer@example.test", true, "Customer From Google"),
        [UnverifiedToken] = new("sub-unverified", "customer@example.test", false, "Unverified"),
        [InactiveToken] = new("sub-inactive", "inactive-manager@example.test", true, "Inactive"),
        [OtherSubjectToken] = new("sub-other", "customer@example.test", true, "Other"),
        [NoNameToken] = new("sub-no-name", "only.prefix@example.test", true, null),
    };

    [Fact]
    public async Task Valid_token_for_an_unknown_email_creates_a_customer_and_returns_our_jwt()
    {
        using var host = new GoogleHost();
        var client = host.Client;

        using var response = await client.PostAsJsonAsync("/api/auth/google", new { idToken = NewUserToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.Equal("Customer", auth!.User.RoleName);
        Assert.Equal("new.person@gmail.test", auth.User.Email);
        Assert.Equal("Nguyễn Văn Mới", auth.User.FullName);
        Assert.Equal("", auth.User.PhoneNumber);

        using (var scope = host.Factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var user = await context.Users.SingleAsync(u => u.Email == "new.person@gmail.test");
            Assert.Equal("sub-new", user.GoogleSubject);
            Assert.Null(user.PasswordHash);
            Assert.True(user.IsActive);
        }

        // The issued token is our normal JWT and works on authenticated endpoints.
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/users/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", auth.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(me)).StatusCode);

        // Signing in again finds the same user by Google subject.
        using var again = await client.PostAsJsonAsync("/api/auth/google", new { idToken = NewUserToken });
        Assert.Equal(auth.User.UserId, (await again.Content.ReadFromJsonAsync<AuthResponseDto>())!.User.UserId);

        Assert.DoesNotContain(host.Logs, line =>
            line.Contains("new.person", StringComparison.OrdinalIgnoreCase) || line.Contains(NewUserToken));
        Assert.Contains(host.Logs, line => line.Contains($"User {auth.User.UserId} logged in with Google"));
    }

    [Fact]
    public async Task Name_falls_back_to_the_email_prefix()
    {
        using var host = new GoogleHost();

        using var response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = NoNameToken });

        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.Equal("only.prefix", auth!.User.FullName);
    }

    [Fact]
    public async Task Verified_email_links_the_existing_account()
    {
        using var host = new GoogleHost();

        using var response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = ExistingEmailToken });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var auth = await response.Content.ReadFromJsonAsync<AuthResponseDto>();
        Assert.Equal(10, auth!.User.UserId);
        Assert.Equal("Customer A", auth.User.FullName);
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await context.Users.SingleAsync(u => u.UserId == 10);
        Assert.Equal("sub-existing", user.GoogleSubject);
        Assert.NotNull(user.PasswordHash);
        Assert.Equal(1, await context.Users.CountAsync(u => u.Email == "customer@example.test"));
    }

    [Fact]
    public async Task Unverified_email_is_refused_and_nothing_is_linked()
    {
        using var host = new GoogleHost();

        using var response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = UnverifiedToken });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await context.Users.AnyAsync(u => u.GoogleSubject != null));
    }

    [Fact]
    public async Task Inactive_account_is_refused_like_a_locked_password_login()
    {
        using var host = new GoogleHost();

        using var google = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = InactiveToken });
        using var password = await host.Client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "inactive-manager@example.test",
            password = BookingsApiTests.Password
        });

        Assert.Equal(HttpStatusCode.Forbidden, google.StatusCode);
        Assert.Equal(password.StatusCode, google.StatusCode);
        Assert.Equal(await DetailAsync(password), await DetailAsync(google));
        using var scope = host.Factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.Null((await context.Users.SingleAsync(u => u.UserId == 14)).GoogleSubject);
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("")]
    public async Task Invalid_token_returns_401(string idToken)
    {
        using var host = new GoogleHost();

        using var response = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Email_already_linked_to_another_google_account_returns_409()
    {
        using var host = new GoogleHost();
        using var first = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = ExistingEmailToken });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var second = await host.Client.PostAsJsonAsync("/api/auth/google", new { idToken = OtherSubjectToken });

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task Endpoint_returns_503_when_google_client_id_is_not_configured()
    {
        // BookingsApiFactory does not set Google:ClientId, so the real validator is unconfigured.
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/google", new { idToken = NewUserToken });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("detail").GetString();
    }

    private sealed class GoogleHost : IDisposable
    {
        private readonly BookingsApiFactory _baseFactory = new();
        private readonly CapturingLoggerProvider _logs = new();

        public GoogleHost()
        {
            Factory = _baseFactory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IGoogleIdTokenValidator>();
                services.AddSingleton<IGoogleIdTokenValidator>(new FakeGoogleIdTokenValidator());
                services.AddSingleton<ILoggerProvider>(_logs);
            }));
            Client = Factory.CreateClient();
        }

        public WebApplicationFactory<PRN232.Api.Controllers.BookingsController> Factory { get; }
        public HttpClient Client { get; }
        public IReadOnlyCollection<string> Logs => _logs.Lines;

        public void Dispose()
        {
            Client.Dispose();
            Factory.Dispose();
            _baseFactory.Dispose();
        }
    }

    private sealed class FakeGoogleIdTokenValidator : IGoogleIdTokenValidator
    {
        public bool IsConfigured => true;

        public Task<GoogleIdentity?> ValidateAsync(string idToken, CancellationToken cancellationToken) =>
            Task.FromResult(Identities.TryGetValue(idToken, out var identity) ? identity : null);
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly ConcurrentQueue<string> _lines = new();

        public IReadOnlyCollection<string> Lines => _lines.ToArray();

        public ILogger CreateLogger(string categoryName) => new Logger(_lines);

        public void Dispose()
        {
        }

        private sealed class Logger(ConcurrentQueue<string> lines) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter) => lines.Enqueue(formatter(state, exception));
        }
    }
}
