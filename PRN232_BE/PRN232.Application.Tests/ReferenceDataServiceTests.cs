using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class ReferenceDataServiceTests
{
    [Fact]
    public async Task Equipment_reads_and_updates_map_entities_without_changing_existing_fields()
    {
        var entity = new Equipments
        {
            EquipmentId = 4,
            RoomId = 8,
            Name = "Old name",
            Supplier = "Old supplier",
            Status = "Active"
        };
        var repository = new EquipmentRepositoryStub(entity);
        var service = new EquipmentService(repository);

        Assert.Equal(
            [new EquipmentDto(4, 8, "Old name", default, default, "Old supplier", "Active")],
            await service.GetAllAsync(8));
        Assert.Equal(new EquipmentDto(4, 8, "Old name", default, default, "Old supplier", "Active"), await service.GetByIdAsync(4));

        var updated = await service.UpdateAsync(
            4,
            new UpdateEquipmentDto(" New name ", default, default, " supplier ", " Maintained "),
            requestingUserId: 9,
            isAdmin: true);

        Assert.Equal(new EquipmentDto(4, 8, "New name", default, default, "supplier", "Maintained"), updated);
        Assert.Equal(8, entity.RoomId);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Service_catalog_reads_create_and_update_map_to_entities_and_dtos()
    {
        var repository = new ServiceRepositoryStub();
        var service = new ServiceCatalogService(repository);

        Assert.Equal([new ServiceDto(1, "Water", 12m)], await service.GetAllAsync());
        Assert.Equal(new ServiceDto(1, "Water", 12m), await service.GetByIdAsync(1));
        Assert.Equal(new ServiceDto(2, "Electricity", 20m), await service.CreateAsync(new CreateServiceDto(" Electricity ", 20m)));
        Assert.Equal(new ServiceDto(2, "Internet", 30m), await service.UpdateAsync(2, new UpdateServiceDto(" Internet ", 30m)));
        Assert.Equal(2, repository.SaveChangesCallCount);
    }

    private sealed class EquipmentRepositoryStub(Equipments equipment) : IEquipmentRepository
    {
        public int SaveChangesCallCount
        {
            get; private set;
        }

        public Task<List<Equipments>> GetAllAsync(int? roomId) =>
            Task.FromResult(new List<Equipments> { equipment });

        public Task<Equipments?> GetByIdAsync(int id) => Task.FromResult<Equipments?>(equipment);

        public Task<Equipments?> GetTrackedByIdAsync(int id) => Task.FromResult<Equipments?>(equipment);

        public Task<Equipments> AddAsync(Equipments value) => Task.FromResult(value);

        public void Remove(Equipments value)
        {
        }

        public Task<int?> GetOwnerIdAsync(int roomId) => Task.FromResult<int?>(9);

        public Task<bool> RoomExistsAsync(int roomId) => Task.FromResult(true);

        public Task SaveChangesAsync()
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class ServiceRepositoryStub : IServiceRepository
    {
        private readonly List<Service> _services = [new Service { ServiceId = 1, Name = "Water", Price = 12m }];

        public int SaveChangesCallCount
        {
            get; private set;
        }

        public Task<List<Service>> GetAllAsync() => Task.FromResult(_services.ToList());

        public Task<Service?> GetByIdAsync(int id) => Task.FromResult(_services.FirstOrDefault(item => item.ServiceId == id));

        public Task<Service?> GetTrackedByIdAsync(int id) => Task.FromResult(_services.FirstOrDefault(item => item.ServiceId == id));

        public Task<Service> AddAsync(Service service)
        {
            service.ServiceId = 2;
            _services.Add(service);
            return Task.FromResult(service);
        }

        public void Remove(Service service) => _services.Remove(service);

        public Task SaveChangesAsync()
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
