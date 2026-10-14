using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository _repository;
    private readonly IMaintenanceReportImageStorage _imageStorage;
    private readonly TimeProvider _timeProvider;

    public MaintenanceService(
        IMaintenanceRepository repository,
        IMaintenanceReportImageStorage imageStorage,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _imageStorage = imageStorage;
        _timeProvider = timeProvider;
    }

    public Task<List<MaintenanceRequestDto>> GetAllAsync(int userId, string role, string? status, int? assignedTo)
    {
        EnsureKnownRole(role);
        if (status is not null && !MaintenanceRequestStatuses.IsValid(status))
        {
            throw new MaintenanceBusinessRuleException("Status must be Pending, Approved, or Completed.");
        }
        return _repository.GetAllAsync(
            role == RoleNames.Customer ? userId : null,
            role == RoleNames.Manager ? userId : null,
            status,
            assignedTo);
    }

    public async Task<MaintenanceRequestDto?> GetByIdAsync(int id, int userId, string role)
    {
        EnsureKnownRole(role);
        int? customerId = role == RoleNames.Customer ? userId : null;
        int? assignedManagerId = role == RoleNames.Manager ? userId : null;
        var request = await _repository.GetByIdAsync(id, customerId, assignedManagerId);
        if (request is null && await _repository.RequestExistsAsync(id) &&
            (customerId.HasValue || assignedManagerId.HasValue))
        {
            throw new MaintenanceAuthorizationException("You cannot access this maintenance request.");
        }
        return request;
    }

    public async Task<MaintenanceRequestDto> CreateAsync(CreateMaintenanceRequestDto dto, int customerId)
    {
        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            throw new MaintenanceBusinessRuleException("Description is required.");
        }

        var contract = await _repository.GetContractAsync(dto.ContractId);
        if (contract is null)
        {
            throw new MaintenanceNotFoundException("Contract does not exist.");
        }
        if (contract.CustomerId != customerId)
        {
            throw new MaintenanceAuthorizationException("You cannot create a request for another customer's contract.");
        }
        ValidateContractDate(contract);
        if (dto.EquipmentId is int equipmentId)
        {
            await ValidateEquipmentAsync(equipmentId, contract.RoomId);
        }

        var request = new MaintenanceRequests
        {
            ContractId = dto.ContractId,
            EquipmentId = dto.EquipmentId,
            Description = dto.Description,
            Status = MaintenanceRequestStatuses.Pending,
            AssignedTo = null
        };

        await _repository.AddWithInitialLogAsync(request, new MaintenanceLogs
        {
            OldStatus = null,
            NewStatus = MaintenanceRequestStatuses.Pending,
            ChangedBy = customerId,
            ChangedAt = _timeProvider.GetUtcNow().UtcDateTime
        });

        return await _repository.GetByIdAsync(request.RequestId, customerId, null)
            ?? throw new InvalidOperationException("Created maintenance request was not found.");
    }

    public async Task<MaintenanceRequestDto?> UpdateAsync(
        int id,
        UpdateMaintenanceRequestDto dto,
        int actorUserId,
        string role)
    {
        if (role != RoleNames.Admin)
        {
            throw new MaintenanceAuthorizationException("Only admins can assign maintenance requests.");
        }

        var request = await _repository.GetStateByIdAsync(id, null);
        if (request is null)
        {
            return null;
        }

        if (request.Status != MaintenanceRequestStatuses.Pending)
        {
            throw new MaintenanceStatusTransitionException("Only Pending requests can be assigned.");
        }
        if (dto.AssignedTo is null)
        {
            throw new MaintenanceAssigneeException("An active Manager is required for assignment.");
        }

        var manager = await _repository.GetUserAssignmentAsync(dto.AssignedTo.Value);
        if (manager is null)
        {
            throw new MaintenanceNotFoundException("Assignee does not exist.");
        }
        if (!manager.IsActive || manager.RoleName != RoleNames.Manager)
        {
            throw new MaintenanceAssigneeException("Assignee must be an active Manager.");
        }

        var contract = await _repository.GetContractAsync(request.ContractId)
            ?? throw new MaintenanceNotFoundException("Contract does not exist.");
        ValidateContractDate(contract);
        if (request.EquipmentId is int equipmentId)
        {
            await ValidateEquipmentAsync(equipmentId, contract.RoomId);
        }

        var changedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var updated = await _repository.UpdateStatusAndAddLogAsync(
            id, MaintenanceRequestStatuses.Pending, MaintenanceRequestStatuses.Approved,
            dto.AssignedTo, actorUserId, changedAt);
        if (!updated)
        {
            throw new MaintenanceStatusTransitionException("Request status changed before the update completed.");
        }

        return await _repository.GetByIdAsync(id, null, null);
    }

    public async Task<List<MaintenanceLogDto>?> GetLogsAsync(int id, int userId, string role)
    {
        EnsureKnownRole(role);
        int? customerId = role == RoleNames.Customer ? userId : null;
        int? assignedManagerId = role == RoleNames.Manager ? userId : null;
        var logs = await _repository.GetLogsAsync(id, customerId, assignedManagerId);
        if (logs is null && await _repository.RequestExistsAsync(id) &&
            (customerId.HasValue || assignedManagerId.HasValue))
        {
            throw new MaintenanceAuthorizationException("You cannot access these maintenance logs.");
        }
        return logs;
    }

    public async Task<MaintenanceReportDto> CreateReportAsync(
        int id,
        CreateMaintenanceReportDto dto,
        int managerId,
        string role)
    {
        if (role != RoleNames.Manager)
        {
            throw new MaintenanceAuthorizationException("Only Managers can submit completion reports.");
        }
        if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length > 4000)
        {
            throw new MaintenanceBusinessRuleException("Description is required and must not exceed 4000 characters.");
        }
        if (dto.Images.Count is < 1 or > 5)
        {
            throw new MaintenanceBusinessRuleException("Submit between 1 and 5 evidence images.");
        }
        foreach (var image in dto.Images)
        {
            ValidateImage(image);
        }

        var request = await _repository.GetStateByIdAsync(id, managerId);
        if (request is null)
        {
            if (await _repository.RequestExistsAsync(id))
            {
                throw new MaintenanceAuthorizationException("This request is not assigned to you.");
            }
            throw new MaintenanceNotFoundException("Maintenance request does not exist.");
        }
        if (request.Status != MaintenanceRequestStatuses.Approved)
        {
            throw new MaintenanceStatusTransitionException("Only Approved requests can be completed.");
        }
        if (await _repository.ReportExistsAsync(id))
        {
            throw new MaintenanceStatusTransitionException("A completion report already exists for this request.");
        }

        var uploadedPaths = new List<string>();
        var databaseCommitted = false;
        try
        {
            foreach (var image in dto.Images)
            {
                uploadedPaths.Add(await _imageStorage.UploadAsync(id, image));
            }

            var report = new MaintenanceReports
            {
                ReportedBy = managerId,
                Description = dto.Description.Trim(),
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                Images = uploadedPaths.Select(path => new MaintenanceReportImages { ImageUrl = path }).ToList()
            };
            if (!await _repository.CompleteWithReportAsync(id, report, managerId, report.CreatedAt))
            {
                throw new MaintenanceStatusTransitionException("Request status changed before completion.");
            }
            databaseCommitted = true;

            var createdReport = await _repository.GetReportAsync(id)
                ?? throw new InvalidOperationException("Created maintenance report was not found.");
            return await AddReadUrlsAsync(createdReport);
        }
        catch
        {
            foreach (var path in uploadedPaths.Where(_ => !databaseCommitted))
            {
                try { await _imageStorage.DeleteAsync(path); }
                catch { }
            }
            throw;
        }
    }

    public async Task<MaintenanceReportDto?> GetReportAsync(int id, string role)
    {
        if (role != RoleNames.Admin)
        {
            throw new MaintenanceAuthorizationException("Only Admins can view completion reports.");
        }
        var report = await _repository.GetReportAsync(id);
        if (report is null)
        {
            return null;
        }
        return await AddReadUrlsAsync(report);
    }

    private async Task<MaintenanceReportDto> AddReadUrlsAsync(MaintenanceReportDto report)
    {
        var images = new List<MaintenanceReportImageDto>(report.Images.Count);
        foreach (var image in report.Images)
        {
            images.Add(image with { ImageUrl = await _imageStorage.GetReadUrlAsync(image.ImageUrl) });
        }
        return report with { Images = images };
    }

    private static void EnsureKnownRole(string role)
    {
        if (role is not (RoleNames.Customer or RoleNames.Manager or RoleNames.Admin))
        {
            throw new MaintenanceAuthorizationException("User role is not authorized for maintenance requests.");
        }
    }

    private static void ValidateImage(MaintenanceReportImageInput image)
    {
        if (image.Content.Length is 0 or > 5 * 1024 * 1024)
        {
            throw new MaintenanceBusinessRuleException("Each image must be between 1 byte and 5 MiB.");
        }

        var isJpeg = image.ContentType == "image/jpeg" && image.Content.Length >= 4 &&
                     image.Content.AsSpan().StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }) &&
                     image.Content.AsSpan(image.Content.Length - 2).SequenceEqual(new byte[] { 0xFF, 0xD9 });
        var isPng = image.ContentType == "image/png" && image.Content.Length >= 20 &&
                    image.Content.AsSpan().StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
                    image.Content.AsSpan(image.Content.Length - 8).SequenceEqual(new byte[] { 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82 });
        var isWebp = image.ContentType == "image/webp" && image.Content.Length >= 20 &&
                     image.Content.AsSpan(0, 4).SequenceEqual("RIFF"u8) && image.Content.AsSpan(8, 4).SequenceEqual("WEBP"u8) &&
                     System.Buffers.Binary.BinaryPrimitives.ReadUInt32LittleEndian(image.Content.AsSpan(4, 4)) == image.Content.Length - 8;
        if (!isJpeg && !isPng && !isWebp)
        {
            throw new MaintenanceBusinessRuleException("Evidence must contain actual JPEG, PNG, or WebP image data.");
        }
    }

    private void ValidateContractDate(MaintenanceContractDto contract)
    {
        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (today < contract.StartDate || today > contract.EndDate)
        {
            throw new MaintenanceContractException("Contract is outside its valid date range.");
        }

        // Contract status vocabulary is not established in this repository.
    }

    private async Task ValidateEquipmentAsync(int equipmentId, int contractRoomId)
    {
        var equipmentRoomId = await _repository.GetEquipmentRoomIdAsync(equipmentId);
        if (equipmentRoomId is null)
        {
            throw new MaintenanceNotFoundException("Equipment does not exist.");
        }
        if (equipmentRoomId.Value != contractRoomId)
        {
            throw new MaintenanceEquipmentException("Equipment does not belong to the contract room.");
        }
    }
}

public sealed class MaintenanceAuthorizationException(string message) : Exception(message);
