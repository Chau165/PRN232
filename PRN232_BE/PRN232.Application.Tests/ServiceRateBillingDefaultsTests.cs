using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ServiceRateBillingDefaultsTests
{
    [Fact]
    public void Additional_occupant_defaults_to_billing_only_roommates()
    {
        var billingDefault = ServiceRateBillingDefaults.ForServiceName("Additional Occupant");

        Assert.Equal(new ServiceRateBillingDefault("person_month", "additional_occupant"), billingDefault);
    }

    [Fact]
    public async Task Service_catalog_returns_the_additional_occupant_default_to_clients()
    {
        var repository = new FakeServiceRepository(
            [new Service { ServiceId = 8, Name = "Additional Occupant", Price = 0m }]);
        var serviceCatalog = new ServiceCatalogService(repository);

        var services = await serviceCatalog.GetAllAsync();

        var additionalOccupant = Assert.Single(services);
        Assert.Equal(8, additionalOccupant.ServiceId);
        Assert.Equal(("person_month", "additional_occupant"),
            (additionalOccupant.DefaultUnitCode, additionalOccupant.DefaultBillingBasis));
    }

    private sealed class FakeServiceRepository : IServiceRepository
    {
        private readonly List<Service> _services;

        public FakeServiceRepository(IEnumerable<Service> services)
        {
            _services = services.ToList();
        }

        public Task<List<Service>> GetAllAsync() => Task.FromResult(_services);

        public Task<Service?> GetByIdAsync(int id) =>
            Task.FromResult(_services.SingleOrDefault(service => service.ServiceId == id));

        public Task<Service?> GetTrackedByIdAsync(int id) => GetByIdAsync(id);

        public Task<Service> AddAsync(Service service)
        {
            _services.Add(service);
            return Task.FromResult(service);
        }

        public void Remove(Service service) => _services.Remove(service);

        public Task SaveChangesAsync() => Task.CompletedTask;
    }
}
