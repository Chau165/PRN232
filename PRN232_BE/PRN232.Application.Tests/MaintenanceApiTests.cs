using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PRN232.Api.Controllers;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public class MaintenanceApiTests
{
    [Theory]
    [InlineData("/api/maintenance-requests")]
    [InlineData("/api/maintenance-requests/1")]
    [InlineData("/api/maintenance-requests/1/logs")]
    public async Task Anonymous_maintenance_reads_require_authentication(string path)
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(path)).StatusCode);
    }

    private sealed class ApiFactory : WebApplicationFactory<MaintenanceRequestsController>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Testing");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                services.AddDbContext<AppDbContext>(options => options.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            });
        }
    }
}
