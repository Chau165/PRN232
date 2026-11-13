using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Api.Controllers;
using PRN232.Application.Services;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

// A12: health check DB phải trả mã lỗi khi DB không kết nối được, vì Railway chỉ đọc mã HTTP.
public sealed class HealthChecksControllerTests
{
    [Fact]
    public async Task Database_check_returns_200_when_database_is_reachable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options);

        var result = await CreateController(context).GetDb(CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
    }

    [Fact]
    public async Task Database_check_returns_503_when_database_is_unreachable()
    {
        // Cổng 1 trên loopback không có PostgreSQL nào lắng nghe: kết nối bị từ chối ngay.
        await using var context = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=postgres;Username=postgres;Password=unused;Timeout=2")
            .Options);

        var result = await CreateController(context).GetDb(CancellationToken.None);

        var status = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, status.StatusCode);
    }

    private static HealthChecksController CreateController(AppDbContext context)
    {
        return new HealthChecksController(new HealthCheckService(new HealthCheckRepository(context)));
    }
}
