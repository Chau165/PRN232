using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using PRN232.Application.Common;
using PRN232.Application.DTOs;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class UsersPagingApiTests
{
    // BookingsApiFactory seeds 8 users (ids 7, 8, 9, 10, 11, 12, 14, 20).
    private const int SeededUsers = 8;

    [Theory]
    [InlineData(null, null, 1, 20)]
    [InlineData(0, 0, 1, 20)]
    [InlineData(-3, -1, 1, 20)]
    [InlineData(3, 50, 3, 50)]
    [InlineData(2, 101, 2, 100)]
    [InlineData(1, 5000, 1, 100)]
    public void Page_request_applies_defaults_and_caps_page_size(
        int? page, int? pageSize, int expectedPage, int expectedPageSize)
    {
        var request = PageRequest.Create(page, pageSize);

        Assert.Equal(expectedPage, request.Page);
        Assert.Equal(expectedPageSize, request.PageSize);
        Assert.Equal((expectedPage - 1) * expectedPageSize, request.Skip);
    }

    [Fact]
    public void Page_request_skip_does_not_overflow_for_huge_page_numbers()
    {
        Assert.True(PageRequest.Create(int.MaxValue, 100).Skip >= 0);
    }

    [Fact]
    public async Task Second_page_returns_the_next_users_in_stable_id_order()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        var first = await client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?page=1&pageSize=3");
        var second = await client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?page=2&pageSize=3");

        Assert.Equal([7, 8, 9], first!.Items.Select(user => user.UserId));
        Assert.Equal([10, 11, 12], second!.Items.Select(user => user.UserId));
        Assert.Equal(2, second.Page);
        Assert.Equal(3, second.PageSize);
        Assert.Equal(SeededUsers, second.TotalCount);
    }

    [Fact]
    public async Task Page_size_above_100_is_capped_and_total_count_covers_all_rows()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        using (var scope = factory.Services.CreateScope())
        {
            var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            context.Users.AddRange(Enumerable.Range(1000, 120).Select(id => new Users
            {
                UserId = id,
                RoleId = 1,
                FullName = $"Customer {id}",
                Email = $"customer{id}@example.test",
                PasswordHash = "unused",
                PhoneNumber = "",
                IsActive = true
            }));
            await context.SaveChangesAsync();
        }
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        var page = await client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?pageSize=500");

        Assert.Equal(1, page!.Page);
        Assert.Equal(100, page.PageSize);
        Assert.Equal(100, page.Items.Count);
        Assert.Equal(SeededUsers + 120, page.TotalCount);
    }

    [Fact]
    public async Task Role_filter_is_applied_before_counting()
    {
        using var factory = new BookingsApiFactory();
        using var client = factory.CreateClient();
        await BookingsApiTests.LoginAsync(client, "admin@example.test");

        var managers = await client.GetFromJsonAsync<PagedResult<UserDto>>("/api/users?roleId=2");

        Assert.Equal([7, 8, 14], managers!.Items.Select(user => user.UserId));
        Assert.Equal(3, managers.TotalCount);
        Assert.Equal(20, managers.PageSize);
    }
}
