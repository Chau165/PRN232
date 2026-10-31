using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using PRN232.Domain.Entities;
using PRN232.Infrastructure.Data;
using PRN232.Infrastructure.Repositories;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class AmenityRepositoryTests
{
    [Fact]
    public async Task Read_methods_return_ordered_entities_without_tracking_and_write_lookup_tracks()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;
        await using var context = new AppDbContext(options);
        await context.Database.EnsureCreatedAsync();
        context.Amenities.AddRange(
            new Amenities { Name = "Wi-Fi" },
            new Amenities { Name = "Balcony" });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
        var repository = new AmenityRepository(context);

        var all = await repository.GetAllAsync();
        var byId = await repository.GetByIdAsync(all[0].AmenityId);

        Assert.Equal(["Balcony", "Wi-Fi"], all.Select(amenity => amenity.Name));
        Assert.All(all, amenity => Assert.Equal(EntityState.Detached, context.Entry(amenity).State));
        Assert.NotNull(byId);
        Assert.Equal(EntityState.Detached, context.Entry(byId).State);

        var tracked = await repository.GetTrackedByIdAsync(all[0].AmenityId);

        Assert.NotNull(tracked);
        Assert.Equal(EntityState.Unchanged, context.Entry(tracked).State);
    }
}
