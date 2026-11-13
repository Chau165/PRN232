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
        var service = new IdentityDocumentService(repository, new FakeCccdHasher());

        Assert.Equal([ToDto(repository.ReadModel, Masked)], await service.GetAllAsync(null, 1, "Admin"));
        Assert.Equal([ToDto(repository.ReadModel, Masked)], await service.GetMineAsync(7));
        Assert.Equal(ToDto(repository.ReadModel, FullNumber), await service.GetByIdAsync(4, 1, "Admin"));
    }

    [Fact]
    public async Task Full_cccd_is_returned_only_to_an_authorized_reviewer_opening_the_detail()
    {
        var repository = new IdentityDocumentRepositoryStub();
        var service = new IdentityDocumentService(repository, new FakeCccdHasher());

        Assert.All(await service.GetAllAsync(null, 1, "Admin"), dto => Assert.Equal(Masked, dto.CCCDNumber));
        Assert.All(await service.GetAllAsync(null, 30, "Manager"), dto => Assert.Equal(Masked, dto.CCCDNumber));
        Assert.All(await service.GetMineAsync(7), dto => Assert.Equal(Masked, dto.CCCDNumber));
        Assert.Equal(Masked, (await service.GetByIdAsync(4, 7, "Customer"))!.CCCDNumber);
        Assert.Equal(FullNumber, (await service.GetByIdAsync(4, 1, "Admin"))!.CCCDNumber);
        Assert.Equal(FullNumber, (await service.GetByIdAsync(4, 30, "Manager"))!.CCCDNumber);

        repository.CustomerBelongsToManager = false;
        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.GetByIdAsync(4, 30, "Manager"));
    }

    [Theory]
    [InlineData("012345678901", "************8901")]
    [InlineData("  012345678901  ", "************8901")]
    [InlineData("1234", "************")]
    [InlineData("", "************")]
    [InlineData(null, "************")]
    public void Masking_keeps_only_the_last_four_digits(string? input, string expected)
    {
        Assert.Equal(expected, PRN232.Application.Policies.CccdMasking.MaskNumber(input));
    }

    [Fact]
    public async Task Submit_checks_duplicates_by_hash_and_stores_the_hash()
    {
        var repository = new IdentityDocumentRepositoryStub { UsedHash = "hash:012345678901" };
        var service = new IdentityDocumentService(repository, new FakeCccdHasher());

        var exception = await Assert.ThrowsAsync<IdentityDocumentDuplicateException>(
            () => service.SubmitAsync(new SubmitIdentityDocumentDto("012345678901"), 7));
        Assert.DoesNotContain("012345678901", exception.Message);
        Assert.Contains("8901", exception.Message);

        var created = await service.SubmitAsync(new SubmitIdentityDocumentDto("098765432109"), 7);
        Assert.Equal("hash:098765432109", repository.AddedDocument!.CCCDNumberHash);
        Assert.Equal(Masked, created.CCCDNumber);
    }

    [Fact]
    public async Task List_is_unscoped_for_admin_scoped_to_manager_and_denied_for_customer()
    {
        var repository = new IdentityDocumentRepositoryStub();
        var service = new IdentityDocumentService(repository, new FakeCccdHasher());

        await service.GetAllAsync("Pending", 1, "Admin");
        Assert.Null(repository.LastManagerId);

        await service.GetAllAsync("Pending", 30, "Manager");
        Assert.Equal(30, repository.LastManagerId);

        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.GetAllAsync(null, 7, "Customer"));
    }

    [Fact]
    public async Task Get_by_id_allows_assigned_manager_and_the_customer_only()
    {
        var repository = new IdentityDocumentRepositoryStub { CustomerBelongsToManager = false };
        var service = new IdentityDocumentService(repository, new FakeCccdHasher());

        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.GetByIdAsync(4, 30, "Manager"));
        await Assert.ThrowsAsync<IdentityDocumentAuthorizationException>(
            () => service.GetByIdAsync(4, 99, "Customer"));
        Assert.NotNull(await service.GetByIdAsync(4, 7, "Customer"));

        repository.CustomerBelongsToManager = true;
        Assert.NotNull(await service.GetByIdAsync(4, 30, "Manager"));
    }

    [Fact]
    public async Task Manager_cannot_review_document_of_customer_outside_their_properties()
    {
        var repository = new IdentityDocumentRepositoryStub
        {
            CustomerBelongsToManager = false,
            TrackedDocument = new IdentityDocuments { IdentityDocumentId = 4, CustomerId = 7, CCCDNumber = "012345678901", Status = "Pending" }
        };
        var service = new IdentityDocumentService(repository, new FakeCccdHasher());

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
        var service = new IdentityDocumentService(repository, new FakeCccdHasher());

        var result = await service.SubmitAsync(new SubmitIdentityDocumentDto("  012345678901  "), 7);

        Assert.Equal(ToDto(repository.ReadModel, Masked), result);
        Assert.Equal("hash:012345678901", repository.AddedDocument?.CCCDNumberHash);
        Assert.Equal("012345678901", repository.AddedDocument?.CCCDNumber);
        Assert.Equal("Pending", repository.AddedDocument?.Status);
        Assert.Equal(7, repository.AddedDocument?.CustomerId);
        Assert.Equal(1, repository.SaveChangesCallCount);
    }

    private const string FullNumber = "012345678901";
    private const string Masked = "************8901";

    private static IdentityDocumentDto ToDto(IdentityDocumentReadModel readModel, string cccdNumber) =>
        new(
            readModel.IdentityDocumentId,
            readModel.CustomerId,
            readModel.CustomerName,
            cccdNumber,
            readModel.Status,
            readModel.ReviewedBy,
            readModel.ReviewedByName);

    private sealed class FakeCccdHasher : ICccdHasher
    {
        public string Hash(string cccdNumber) => $"hash:{cccdNumber.Trim()}";
    }

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

        public int? LastManagerId
        {
            get; private set;
        }

        public bool CustomerBelongsToManager
        {
            get; set;
        } = true;

        public IdentityDocuments? TrackedDocument
        {
            get; set;
        }

        public Task<List<IdentityDocumentReadModel>> GetAllAsync(string? status, int? managerId)
        {
            LastManagerId = managerId;
            return Task.FromResult(new List<IdentityDocumentReadModel> { ReadModel });
        }

        public Task<bool> IsCustomerOfManagerAsync(int customerId, int managerId) =>
            Task.FromResult(CustomerBelongsToManager);

        public Task<List<IdentityDocumentReadModel>> GetByCustomerIdAsync(int customerId) =>
            Task.FromResult(new List<IdentityDocumentReadModel> { ReadModel });

        public Task<IdentityDocumentReadModel?> GetByIdAsync(int id) => Task.FromResult<IdentityDocumentReadModel?>(ReadModel);

        public Task<IdentityDocuments?> GetTrackedByIdAsync(int id) => Task.FromResult(TrackedDocument);

        public Task<bool> HasPendingOrApprovedAsync(int customerId) => Task.FromResult(false);

        public string? UsedHash { get; set; }

        public Task<bool> CccdNumberUsedByOtherAsync(string cccdNumberHash, int customerId) =>
            Task.FromResult(cccdNumberHash == UsedHash);

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
