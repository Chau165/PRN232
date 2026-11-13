using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// Accounts created through Google sign-in have no password (PasswordHash is null).
public sealed class PasswordlessAccountLoginTests
{
    [Fact]
    public async Task Password_login_for_an_account_without_password_fails_like_a_wrong_password()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.Add(new Users
            {
                UserId = 40,
                RoleId = 1,
                FullName = "Google User",
                Email = "google.user@example.test",
                PasswordHash = null,
                GoogleSubject = "google-sub-40",
                PhoneNumber = "",
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        using var passwordless = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "google.user@example.test",
            password = "anything-123"
        });
        using var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "customer@example.test",
            password = "wrong-password-123"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, passwordless.StatusCode);
        Assert.Equal(wrongPassword.StatusCode, passwordless.StatusCode);
        Assert.Equal(await DetailAsync(wrongPassword), await DetailAsync(passwordless));
    }

    [Fact]
    public async Task Google_subject_is_unique_and_password_hash_is_optional_in_the_model()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = context.Model.FindEntityType(typeof(Users))!;

        Assert.True(user.FindProperty(nameof(Users.PasswordHash))!.IsNullable);
        Assert.True(user.FindProperty(nameof(Users.GoogleSubject))!.IsNullable);
        var index = Assert.Single(user.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual([nameof(Users.GoogleSubject)]));
        Assert.True(index.IsUnique);

        context.Users.AddRange(
            new Users { UserId = 41, RoleId = 1, FullName = "A", Email = "a@example.test", GoogleSubject = "same-sub", IsActive = true },
            new Users { UserId = 42, RoleId = 1, FullName = "B", Email = "b@example.test", GoogleSubject = "same-sub", IsActive = true });
        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("detail").GetString();
    }
}
