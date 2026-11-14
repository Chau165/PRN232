using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace PRN232.Application.Tests;

// Rebuild 1.1: every FluentValidation validator registered in DI runs before the action and
// answers 400 with all field errors, keyed by the JSON (camelCase) field names the client sent.
public sealed class FluentValidationFilterTests
{
    [Fact]
    public async Task Invalid_body_returns_every_field_error_keyed_by_json_name()
    {
        using var factory = new MaintenanceApiFactory();
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/auth/register", new
        {
            fullName = "A",
            email = "not-an-email",
            password = "abc",
            phoneNumber = "0901234567"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Dữ liệu không hợp lệ.", problem.GetProperty("title").GetString());
        // detail keeps the first message so screens that only show one message still work.
        Assert.Equal("Họ tên phải từ 2 đến 100 ký tự.", problem.GetProperty("detail").GetString());

        var errors = problem.GetProperty("errors");
        Assert.Equal("Họ tên phải từ 2 đến 100 ký tự.", errors.GetProperty("fullName")[0].GetString());
        Assert.Equal("Email không hợp lệ.", errors.GetProperty("email")[0].GetString());
        Assert.Equal("Mật khẩu phải có ít nhất 8 ký tự.", errors.GetProperty("password")[0].GetString());
        Assert.False(errors.TryGetProperty("phoneNumber", out _));
    }
}
