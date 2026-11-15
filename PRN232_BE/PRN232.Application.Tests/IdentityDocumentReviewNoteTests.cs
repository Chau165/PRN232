using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.DTOs;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

// V3: rejecting a CCCD needs a reason the customer can see.
public sealed class IdentityDocumentReviewNoteTests
{
    [Fact]
    public async Task Reject_without_a_note_is_refused_and_nothing_changes()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var documentId = await SubmitAsync(client);
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        using var blank = await client.PutAsJsonAsync($"/api/identity-documents/{documentId}/review",
            new { status = "Rejected", note = "   " });
        using var missing = await client.PutAsJsonAsync($"/api/identity-documents/{documentId}/review",
            new { status = "Rejected" });

        Assert.Equal(HttpStatusCode.BadRequest, blank.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("Vui lòng nhập lý do từ chối hồ sơ.", await DetailAsync(missing));
        using var scope = factory.Services.CreateScope();
        var document = await scope.ServiceProvider.GetRequiredService<AppDbContext>()
            .IdentityDocuments.SingleAsync(d => d.IdentityDocumentId == documentId);
        Assert.Equal("Pending", document.Status);
        Assert.Null(document.ReviewNote);
    }

    [Fact]
    public async Task Reject_with_a_note_stores_it_and_shows_it_to_the_customer()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var documentId = await SubmitAsync(client);
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        using var rejected = await client.PutAsJsonAsync($"/api/identity-documents/{documentId}/review",
            new { status = "Rejected", note = "  Ảnh CCCD bị mờ, vui lòng nộp lại.  " });

        Assert.Equal(HttpStatusCode.OK, rejected.StatusCode);
        Assert.Equal("Ảnh CCCD bị mờ, vui lòng nộp lại.",
            (await rejected.Content.ReadFromJsonAsync<IdentityDocumentDto>())!.ReviewNote);
        var detail = await client.GetFromJsonAsync<IdentityDocumentDto>($"/api/identity-documents/{documentId}");
        Assert.Equal("Ảnh CCCD bị mờ, vui lòng nộp lại.", detail!.ReviewNote);

        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        var mine = Assert.Single((await client.GetFromJsonAsync<IdentityDocumentDto[]>("/api/identity-documents/mine"))!);
        Assert.Equal("Rejected", mine.Status);
        Assert.Equal("Ảnh CCCD bị mờ, vui lòng nộp lại.", mine.ReviewNote);
    }

    [Fact]
    public async Task Approve_without_a_note_is_accepted_and_keeps_no_note()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var documentId = await SubmitAsync(client);
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        using var approved = await client.PutAsJsonAsync($"/api/identity-documents/{documentId}/review",
            new { status = "Approved" });

        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        var dto = await approved.Content.ReadFromJsonAsync<IdentityDocumentDto>();
        Assert.Equal("Approved", dto!.Status);
        Assert.Null(dto.ReviewNote);
    }

    [Fact]
    public async Task Note_longer_than_500_characters_is_refused()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        var documentId = await SubmitAsync(client);
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        using var response = await client.PutAsJsonAsync($"/api/identity-documents/{documentId}/review",
            new { status = "Rejected", note = new string('x', 501) });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("Lý do tối đa 500 ký tự.", await DetailAsync(response));
    }

    private static async Task<int> SubmitAsync(HttpClient client)
    {
        await BookingsApiTests.LoginAsync(client, "customer@example.test");
        using var response = await client.PostAsJsonAsync("/api/identity-documents", new { cccdNumber = "079201001234" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IdentityDocumentDto>())!.IdentityDocumentId;
    }

    private static async Task<string?> DetailAsync(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("detail").GetString();
    }
}
