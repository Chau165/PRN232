using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.Services;
using PRN232.Domain.Entities;
using Xunit;

namespace PRN232.Application.Tests;

public sealed class IdentityDocumentServiceTests
{
    [Fact]
    public async Task Read_methods_map_projected_identity_document_data_to_response_dtos()
    {
        var repository = new IdentityDocumentRepositoryStub();
        var service = new IdentityDocumentService(repository);

        Assert.Equal([ToDto(repository.ReadModel)], await service.GetAllAsync(null, 1, "Admin"));
        Assert.Equal([ToDto(repository.ReadModel)], await service.GetMineAsync(7));
        Assert.Equal(ToDto(repository.ReadModel), await service.GetByIdAsync(4, 1, "Admin"));
    }

    [Fact]
    public async Task List_is_unscoped_for_admin_scoped_to_owner_for_manager_and_denied_for_customer()
    {
        var repository = new IdentityDocumentRepositoryStub();
        var service = new IdentityDocumentService(repository);

        await service.GetAllAsync("Pending", 1, "Admin");
        Assert.Null(repository.LastOwnerId);

        await service.GetAllAsync("Pending", 30, "Manager");
        Assert.Equal(30, repository.LastOwnerId);

        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.GetAllAsync(null, 7, "Customer"));
    }

    [Fact]
    public async Task Get_by_id_allows_owner_manager_and_the_customer_only()
    {
        var repository = new IdentityDocumentRepositoryStub { CustomerBelongsToOwner = false };
        var service = new IdentityDocumentService(repository);

        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.GetByIdAsync(4, 30, "Manager"));
        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.GetByIdAsync(4, 99, "Customer"));
        Assert.NotNull(await service.GetByIdAsync(4, 7, "Customer"));

        repository.CustomerBelongsToOwner = true;
        Assert.NotNull(await service.GetByIdAsync(4, 30, "Manager"));
    }

    [Fact]
    public async Task Manager_cannot_review_document_of_customer_outside_their_properties()
    {
        var repository = new IdentityDocumentRepositoryStub
        {
            CustomerBelongsToOwner = false,
            TrackedDocument = new IdentityDocuments { IdentityDocumentId = 4, CustomerId = 7, CCCDNumber = "012345678901", Status = "Pending" }
        };
        var service = new IdentityDocumentService(repository);

        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.ReviewAsync(4, new ReviewIdentityDocumentDto("Approved"), 30, "Manager"));
        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.ReviewAsync(4, new ReviewIdentityDocumentDto("Approved"), 7, "Customer"));
        Assert.Equal("Pending", repository.TrackedDocument!.Status);
        Assert.Equal(0, repository.SaveChangesCallCount);
    }

    [Fact]
    public async Task Submit_preserves_normalization_status_and_reloads_projected_response()
    {
        var repository = new IdentityDocumentRepositoryStub();
        var service = new IdentityDocumentService(repository);

        var result = await service.SubmitAsync(new SubmitIdentityDocumentDto("  012345678901  "), 7);

        Assert.Equal(ToDto(repository.ReadModel), result);
        Assert.Equal("012345678901", repository.AddedDocument?.CCCDNumber);
        Assert.Equal("Pending", repository.AddedDocument?.Status);
        Assert.Equal(7, repository.AddedDocument?.CustomerId);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    private static IdentityDocumentDto ToDto(IdentityDocumentReadModel readModel) =>
        new(
            readModel.IdentityDocumentId,
            readModel.CustomerId,
            readModel.CustomerName,
            readModel.CCCDNumber,
            readModel.Status,
            readModel.ReviewedBy,
            readModel.ReviewedByName);

    private sealed class IdentityDocumentRepositoryStub : IIdentityDocumentRepository
    {
        public IdentityDocumentReadModel ReadModel
        {
            get;
        } = new(
            4,
            7,
            "Customer Name",
            "012345678901",
            "Pending",
            null,
            null);

        public IdentityDocuments? AddedDocument
        {
            get; private set;
        }

        public int SaveChangesCallCount
        {
            get; private set;
        }

        public int? LastOwnerId
        {
            get; private set;
        }

        public bool CustomerBelongsToOwner
        {
            get; set;
        } = true;

        public IdentityDocuments? TrackedDocument
        {
            get; set;
        }

        public Task<List<IdentityDocumentReadModel>> GetAllAsync(string? status, int? ownerId)
        {
            LastOwnerId = ownerId;
            return Task.FromResult(new List<IdentityDocumentReadModel> { ReadModel });
        }

        public Task<bool> IsCustomerOfOwnerAsync(int customerId, int ownerId) =>
            Task.FromResult(CustomerBelongsToOwner);

        public Task<List<IdentityDocumentReadModel>> GetByCustomerIdAsync(int customerId) =>
            Task.FromResult(new List<IdentityDocumentReadModel> { ReadModel });

        public Task<IdentityDocumentReadModel?> GetByIdAsync(int id) => Task.FromResult<IdentityDocumentReadModel?>(ReadModel);

        public Task<IdentityDocuments?> GetTrackedByIdAsync(int id) => Task.FromResult(TrackedDocument);

        public Task<bool> HasPendingOrApprovedAsync(int customerId) => Task.FromResult(false);

        public Task<bool> CccdNumberUsedByOtherAsync(string cccdNumber, int customerId) => Task.FromResult(false);

        public Task<IdentityDocuments> AddAsync(IdentityDocuments document)
        {
            document.IdentityDocumentId = ReadModel.IdentityDocumentId;
            AddedDocument = document;
            return Task.FromResult(document);
        }

        public Task SaveChangesAsync()
        {
            SaveChangesCallCount++;
            return Task.CompletedTask;
        }
    }
}
