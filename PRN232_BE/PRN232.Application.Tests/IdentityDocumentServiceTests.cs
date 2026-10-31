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

        Assert.Equal([ToDto(repository.ReadModel)], await service.GetAllAsync(null));
        Assert.Equal([ToDto(repository.ReadModel)], await service.GetMineAsync(7));
        Assert.Equal(ToDto(repository.ReadModel), await service.GetByIdAsync(4));
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

        public Task<List<IdentityDocumentReadModel>> GetAllAsync(string? status) =>
            Task.FromResult(new List<IdentityDocumentReadModel> { ReadModel });

        public Task<List<IdentityDocumentReadModel>> GetByCustomerIdAsync(int customerId) =>
            Task.FromResult(new List<IdentityDocumentReadModel> { ReadModel });

        public Task<IdentityDocumentReadModel?> GetByIdAsync(int id) => Task.FromResult<IdentityDocumentReadModel?>(ReadModel);

        public Task<IdentityDocuments?> GetTrackedByIdAsync(int id) => Task.FromResult<IdentityDocuments?>(null);

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
