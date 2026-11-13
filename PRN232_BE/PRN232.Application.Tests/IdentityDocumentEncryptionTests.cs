using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Security;
using Xunit;

namespace PRN232.Application.Tests;

// A9: CCCD numbers are encrypted at rest, masked in responses, and revealed only to a reviewer.
public sealed class IdentityDocumentEncryptionTests
{
    private const string Cccd = "012345678901";
    private const string Masked = "************8901";

    [Fact]
    public async Task Submitted_cccd_is_stored_encrypted_and_masked_except_in_reviewer_detail()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");

        using var submit = await client.PostAsJsonAsync("/api/identity-documents", new { cccdNumber = Cccd });
        Assert.Equal(HttpStatusCode.Created, submit.StatusCode);
        var created = await submit.Content.ReadFromJsonAsync<IdentityDocumentDto>();
        Assert.Equal(Masked, created!.CCCDNumber);

        var (stored, hash) = await ReadRawAsync(factory, created.IdentityDocumentId);
        Assert.NotEqual(Cccd, stored);
        Assert.DoesNotContain(Cccd, stored);
        Assert.True(CccdProtection.IsProtected(stored));
        Assert.False(string.IsNullOrEmpty(hash));
        Assert.DoesNotContain(Cccd, hash!);

        var mine = await client.GetFromJsonAsync<IdentityDocumentDto[]>("/api/identity-documents/mine");
        Assert.Equal(Masked, Assert.Single(mine!).CCCDNumber);
        var ownDetail = await client.GetFromJsonAsync<IdentityDocumentDto>(
            $"/api/identity-documents/{created.IdentityDocumentId}");
        Assert.Equal(Masked, ownDetail!.CCCDNumber);

        await BookingsApiTests.LoginAsync(client, "admin@example.test");
        var list = await client.GetFromJsonAsync<IdentityDocumentDto[]>("/api/identity-documents");
        Assert.Equal(Masked, Assert.Single(list!).CCCDNumber);
        var detail = await client.GetFromJsonAsync<IdentityDocumentDto>(
            $"/api/identity-documents/{created.IdentityDocumentId}");
        Assert.Equal(Cccd, detail!.CCCDNumber);
    }

    [Fact]
    public async Task Duplicate_cccd_from_another_account_is_still_rejected()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        using var first = await client.PostAsJsonAsync("/api/identity-documents", new { cccdNumber = Cccd });
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        await BookingsApiTests.LoginAsync(client, "other@example.test");
        using var duplicate = await client.PostAsJsonAsync(
            "/api/identity-documents",
            new { cccdNumber = $"  {Cccd} " });

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.DoesNotContain(Cccd, await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Backfill_encrypts_legacy_plaintext_rows_and_fills_the_hash()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        int legacyId;
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await context.Database.ExecuteSqlRawAsync(
                "INSERT INTO \"IdentityDocuments\" (\"CustomerId\", \"CCCDNumber\", \"Status\") VALUES (10, {0}, 'Pending')",
                Cccd);
            legacyId = await context.IdentityDocuments.Select(d => d.IdentityDocumentId).SingleAsync();

            // Before the backfill the legacy row is still readable through the converter.
            Assert.Equal(Cccd, (await context.IdentityDocuments.AsNoTracking().SingleAsync()).CCCDNumber);
        }

        using (var scope = factory.Services.CreateScope())
        {
            var backfill = scope.ServiceProvider.GetRequiredService<IdentityDocumentEncryptionBackfill>();
            Assert.Equal(1, await backfill.RunAsync());
            Assert.Equal(0, await backfill.RunAsync());
        }

        var (stored, hash) = await ReadRawAsync(factory, legacyId);
        Assert.True(CccdProtection.IsProtected(stored));
        using (var scope = factory.Services.CreateScope())
        {
            var hasher = scope.ServiceProvider.GetRequiredService<ICccdHasher>();
            Assert.Equal(hasher.Hash(Cccd), hash);
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            Assert.Equal(Cccd, (await context.IdentityDocuments.AsNoTracking().SingleAsync()).CCCDNumber);
        }

        // The backfilled hash takes part in the duplicate check.
        await BookingsApiTests.LoginAsync(client, "other@example.test");
        using var duplicate = await client.PostAsJsonAsync("/api/identity-documents", new { cccdNumber = Cccd });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    [Fact]
    public void Hmac_hasher_is_deterministic_keyed_and_ignores_surrounding_whitespace()
    {
        var key = new byte[32];
        var otherKey = Enumerable.Repeat((byte)1, 32).ToArray();
        var hasher = new HmacCccdHasher(key);

        Assert.Equal(hasher.Hash(Cccd), hasher.Hash($" {Cccd} "));
        Assert.NotEqual(hasher.Hash(Cccd), hasher.Hash("012345678902"));
        Assert.NotEqual(hasher.Hash(Cccd), new HmacCccdHasher(otherKey).Hash(Cccd));
        Assert.Throws<ArgumentException>(() => new HmacCccdHasher(new byte[16]));
    }

    private static async Task<(string Stored, string? Hash)> ReadRawAsync(BookingsApiFactory factory, int id)
    {
        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        await using var command = connection.CreateCommand();
        command.CommandText =
            "SELECT \"CCCDNumber\", \"CCCDNumberHash\" FROM \"IdentityDocuments\" WHERE \"IdentityDocumentId\" = $id";
        command.Parameters.AddWithValue("$id", id);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.IsDBNull(1) ? null : reader.GetString(1));
    }

    [Fact]
    public async Task Data_protection_key_ring_is_persisted_in_the_database()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        // Goes through the key manager wired by Program.cs (PersistKeysToDbContext); the test
        // hosts' IDataProtectionProvider is the shared in-memory one from TestDataProtection.
        var keyManager = factory.Services.GetRequiredService<IKeyManager>();

        keyManager.CreateNewKey(DateTimeOffset.UtcNow, DateTimeOffset.UtcNow.AddDays(90));

        using var scope = factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.True(await context.DataProtectionKeys.AnyAsync());
    }
}
