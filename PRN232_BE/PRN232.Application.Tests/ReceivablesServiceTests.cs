using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using Xunit;

namespace PRN232.Application.Tests;

public class ReceivablesServiceTests
{
    [Fact]
    public async Task Vietnam_day_boundary_and_zero_balances_are_handled_consistently()
    {
        // Still November 8 in UTC, already November 9 in Vietnam.
        var service = new ReceivablesService(new Repository(), new Clock());
        var report = await service.GetAsync(null, new DateOnly(2026, 11, 9), 7, "Manager", default);
        Assert.Equal(new DateOnly(2026, 11, 9), report.AsOfDate);
        Assert.Equal(30m, report.OutstandingAmount);
        Assert.Equal(2, report.OutstandingInvoiceCount);
        Assert.Equal(10m, report.OverdueAmount);
        Assert.Equal(20m, report.NotYetDueAmount);
        await Assert.ThrowsAsync<ReceivablesValidationException>(() =>
            service.GetAsync(null, new DateOnly(2026, 11, 8), 7, "Manager", default));
        await Assert.ThrowsAsync<ReceivablesForbiddenException>(() =>
            service.GetAsync(null, null, 10, "Customer", default));
    }

    private class Clock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 8, 18, 0, 0, TimeSpan.Zero);
    }

    private class Repository : IReceivablesRepository
    {
        public Task<bool> IsManagerAssignedAsync(int propertyId, int managerId, CancellationToken cancellationToken) => Task.FromResult(managerId == 7);
        public Task<int?> GetPropertyOwnerAsync(int propertyId, CancellationToken cancellationToken) => Task.FromResult<int?>(7);
        public Task<List<ReceivableReadModel>> GetOutstandingAsync(int? propertyId, int? ownerId, CancellationToken cancellationToken)
        {
            Assert.Equal(7, ownerId);
            return Task.FromResult(new List<ReceivableReadModel>
            {
                new(new DateOnly(2026, 11, 8), 10),
                new(new DateOnly(2026, 11, 9), 20),
                new(new DateOnly(2026, 11, 8), 0)
            });
        }
    }
}
