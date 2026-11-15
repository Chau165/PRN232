using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using PRN232.Application.DTOs;
using PRN232.Application.Validation;
using Xunit;

namespace PRN232.Application.Tests;

// V2: the customer's own CCCD must be exactly 12 digits, the same rule as contract occupants.
public sealed class IdentityDocumentCccdFormatTests
{
    [Theory]
    [InlineData("01234567890")]      // 11 digits
    [InlineData("0123456789012")]    // 13 digits
    [InlineData("01234567890A")]     // letter
    [InlineData("012345 678901")]    // space inside
    [InlineData("0123-4567-8901")]   // separators
    public async Task Submit_rejects_anything_that_is_not_12_digits(string cccdNumber)
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsJsonAsync("/api/identity-documents", new { cccdNumber });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Số CCCD phải gồm đúng 12 chữ số.", body.RootElement.GetProperty("detail").GetString());
        var mine = await client.GetFromJsonAsync<IdentityDocumentDto[]>("/api/identity-documents/mine");
        Assert.Empty(mine!);
    }

    [Fact]
    public async Task Submit_accepts_12_digits_with_surrounding_spaces()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsJsonAsync("/api/identity-documents", new { cccdNumber = "  079201001234 " });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    [Fact]
    public async Task Blank_cccd_keeps_the_empty_message()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "customer@example.test");

        using var response = await client.PostAsJsonAsync("/api/identity-documents", new { cccdNumber = "   " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Số CCCD không được để trống.", body.RootElement.GetProperty("detail").GetString());
    }

    [Fact]
    public void Occupant_validator_uses_the_same_shared_rule()
    {
        var validator = new AddContractOccupantDtoValidator();
        var dto = new AddContractOccupantDto("Người Ở Cùng", null, "01234567890", DateOnly.FromDateTime(DateTime.Today), null);

        var result = validator.Validate(dto);

        Assert.Contains(result.Errors, error => error.ErrorMessage == AccountRules.CccdFormatMessage);
        Assert.True(AccountRules.IsValidCccd(" 079201001234 "));
        Assert.False(AccountRules.IsValidCccd("0792010012345"));
    }
}
