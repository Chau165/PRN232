using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ViewingScheduleModelTests
{
    [Fact]
    public void PostgreSql_model_limits_manager_note_to_500_characters()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=unused;Username=unused")
            .Options;
        using var context = new AppDbContext(options);

        var constraint = Assert.Single(context.GetService<IDesignTimeModel>().Model
            .FindEntityType(typeof(ViewingSchedules))!
            .GetCheckConstraints(),
            check => check.Name == "CK_ViewingSchedules_ManagerNote_500");

        Assert.Equal("\"ManagerNote\" IS NULL OR char_length(\"ManagerNote\") <= 500", constraint.Sql);
    }
}
