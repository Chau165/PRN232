using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.ReadModels;
using PRN232.Application.States;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository _repository;
    private readonly INotificationService _notificationService;
    private readonly IMaintenanceReportImageStorage _imageStorage;
    private readonly TimeProvider _timeProvider;

    public MaintenanceService(
        IMaintenanceRepository repository,
        INotificationService notificationService,
        IMaintenanceReportImageStorage imageStorage,
        TimeProvider timeProvider)
    {
        _repository = repository;
        _notificationService = notificationService;
        _imageStorage = imageStorage;
        _timeProvider = timeProvider;
    }

    public async Task<List<MaintenanceRequestDto>> GetAllAsync(int userId, string role, string? status, int? assignedTo)
    {
        EnsureKnownRole(role);
        if (status is not null && !MaintenanceRequestStatuses.IsValid(status))
        {
            throw new MaintenanceBusinessRuleException("Status must be Pending, Approved, or Completed.");
        }
        int? customerId = null;
        if (role == RoleNames.Customer)
        {
            customerId = userId;
        }
        int? assignedManagerId = null;
        if (role == RoleNames.Manager)
        {
            assignedManagerId = userId;
        }

        var requests = await _repository.GetAllAsync(customerId, assignedManagerId, status, assignedTo);
        return requests.Select(ConvertToDto).ToList();
    }

    public async Task<MaintenanceRequestDto?> GetByIdAsync(int id, int userId, string role)
    {
        EnsureKnownRole(role);
        int? customerId = null;
        if (role == RoleNames.Customer)
        {
            customerId = userId;
        }
        int? assignedManagerId = null;
        if (role == RoleNames.Manager)
        {
            assignedManagerId = userId;
        }
        var request = await _repository.GetByIdAsync(id, customerId, assignedManagerId);
        if (request is null && await _repository.RequestExistsAsync(id) &&
            (customerId.HasValue || assignedManagerId.HasValue))
        {
            throw new MaintenanceAuthorizationException("You cannot access this maintenance request.");
        }
        if (request is null)
        {
            return null;
        }
        return ConvertToDto(request);
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

        var initialStatus = MaintenanceRequestStatuses.Pending;
        var request = ConvertToEntity(dto, initialStatus);

        var adminIds = await _notificationService.GetActiveAdminUserIdsAsync();
        var changedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var initialLog = CreateInitialLogEntity(customerId, initialStatus, changedAt);
        await _repository.AddWithInitialLogAsync(request, initialLog, requestId => _notificationService.BuildMaintenanceNotifications(
                adminIds, $"Có yêu cầu bảo trì mới #{requestId} - Phòng {contract.RoomCode}")
            .Concat(_notificationService.BuildMaintenanceNotifications(
                [customerId], $"Bạn đã gửi yêu cầu sửa chữa #{requestId} cho phòng {contract.RoomCode}. Yêu cầu đang chờ duyệt."))
            .ToArray());

        var created = await _repository.GetByIdAsync(request.RequestId, customerId, null);
        if (created is null)
        {
            throw new InvalidOperationException("Created maintenance request was not found.");
        }
        return ConvertToDto(created);
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

        string? assignmentNote = null;
        if (!string.IsNullOrWhiteSpace(dto.AssignmentNote))
        {
            assignmentNote = dto.AssignmentNote.Trim();
        }
        if (assignmentNote?.Length > 1000)
        {
            throw new MaintenanceBusinessRuleException("Assignment note must not exceed 1000 characters.");
        }

        var contract = await _repository.GetContractAsync(request.ContractId);
        if (contract is null)
        {
            throw new MaintenanceNotFoundException("Contract does not exist.");
        }
        ValidateContractDate(contract);
        if (request.EquipmentId is int equipmentId)
        {
            await ValidateEquipmentAsync(equipmentId, contract.RoomId);
        }

        var changedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var notifications = _notificationService.BuildMaintenanceNotifications(
                [contract.CustomerId], $"Yêu cầu bảo trì #{id} - Phòng {contract.RoomCode} đã được duyệt")
            .Concat(_notificationService.BuildMaintenanceNotifications(
                [dto.AssignedTo.Value], $"Bạn được giao yêu cầu bảo trì #{id} - Phòng {contract.RoomCode}"))
            .ToArray();
        var updated = await _repository.UpdateStatusAndAddLogAsync(
            id, MaintenanceRequestStatuses.Pending, MaintenanceRequestStatuses.Approved,
            dto.AssignedTo, assignmentNote, actorUserId, changedAt, notifications);
        if (!updated)
        {
            throw new MaintenanceStatusTransitionException("Request status changed before the update completed.");
        }

        var updatedRequest = await _repository.GetByIdAsync(id, null, null);
        if (updatedRequest is null)
        {
            return null;
        }
        return ConvertToDto(updatedRequest);
    }

    public async Task<List<MaintenanceLogDto>?> GetLogsAsync(int id, int userId, string role)
    {
        EnsureKnownRole(role);
        int? customerId = null;
        if (role == RoleNames.Customer)
        {
            customerId = userId;
        }
        int? assignedManagerId = null;
        if (role == RoleNames.Manager)
        {
            assignedManagerId = userId;
        }
        var logs = await _repository.GetLogsAsync(id, customerId, assignedManagerId);
        if (logs is null && await _repository.RequestExistsAsync(id) &&
            (customerId.HasValue || assignedManagerId.HasValue))
        {
            throw new MaintenanceAuthorizationException("You cannot access these maintenance logs.");
        }
        return logs?.Select(ConvertToDto).ToList();
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

        var requestDetails = await _repository.GetByIdAsync(id, null, null);
        if (requestDetails is null)
        {
            throw new MaintenanceNotFoundException("Maintenance request does not exist.");
        }
        var approvingAdminId = await _repository.GetApprovingAdminIdAsync(id);
        List<int> adminRecipients;
        if (approvingAdminId.HasValue)
        {
            adminRecipients = [approvingAdminId.Value];
        }
        else
        {
            adminRecipients = await _notificationService.GetActiveAdminUserIdsAsync();
        }
        var completionNotifications = _notificationService.BuildMaintenanceNotifications(
                [requestDetails.CustomerId], $"Yêu cầu bảo trì #{id} - Phòng {requestDetails.RoomCode} đã hoàn tất")
            .Concat(_notificationService.BuildMaintenanceNotifications(
                adminRecipients, $"Yêu cầu bảo trì #{id} - Phòng {requestDetails.RoomCode} đã được hoàn tất"))
            .ToArray();

        var uploadedPaths = new List<string>();
        var databaseCommitted = false;
        try
        {
            foreach (var image in dto.Images)
            {
                uploadedPaths.Add(await _imageStorage.UploadAsync(id, image));
            }

            var reportCreatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            var report = ConvertToEntity(dto, managerId, reportCreatedAt, uploadedPaths);
            if (!await _repository.CompleteWithReportAsync(
                    id, report, managerId, report.CreatedAt, completionNotifications))
            {
                throw new MaintenanceStatusTransitionException("Request status changed before completion.");
            }
            databaseCommitted = true;

            var createdReport = await _repository.GetReportAsync(id);
            if (createdReport is null)
            {
                throw new InvalidOperationException("Created maintenance report was not found.");
            }
            return await AddReadUrlsAsync(createdReport);
        }
        catch
        {
            foreach (var path in uploadedPaths.Where(_ => !databaseCommitted))
            {
                try
                {
                    await _imageStorage.DeleteAsync(path);
                }
                catch
                {
                    // Cleanup is best-effort so the original failure remains the one returned.
                }
            }
            throw;
        }
    }

    public async Task<MaintenanceReportDto?> GetReportAsync(int id, int userId, string role)
    {
        if (role == RoleNames.Manager)
        {
            var assignedRequest = await _repository.GetStateByIdAsync(id, userId);
            if (assignedRequest is null)
            {
                if (await _repository.RequestExistsAsync(id))
                {
                    throw new MaintenanceAuthorizationException("This request is not assigned to you.");
                }
                return null;
            }
        }
        else if (role != RoleNames.Admin)
        {
            throw new MaintenanceAuthorizationException("Only Admins and assigned Managers can view completion reports.");
        }

        var report = await _repository.GetReportAsync(id);
        if (report is null)
        {
            return null;
        }
        return await AddReadUrlsAsync(report);
    }

    private async Task<MaintenanceReportDto> AddReadUrlsAsync(MaintenanceReportReadModel report)
    {
        var images = new List<MaintenanceReportImageDto>(report.Images.Count);
        foreach (var image in report.Images)
        {
            images.Add(ConvertToDto(image,
                await _imageStorage.GetReadUrlAsync(image.ImageUrl)));
        }
        return ConvertToDto(report, images);
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

    private static MaintenanceRequests ConvertToEntity(
        CreateMaintenanceRequestDto dto,
        string initialStatus)
    {
        return new MaintenanceRequests
        {
            ContractId = dto.ContractId,
            EquipmentId = dto.EquipmentId,
            Description = dto.Description,
            Status = initialStatus,
            AssignedTo = null
        };
    }

    private static MaintenanceLogs CreateInitialLogEntity(int customerId, string initialStatus, DateTime changedAt)
    {
        return new MaintenanceLogs
        {
            OldStatus = null,
            NewStatus = initialStatus,
            ChangedBy = customerId,
            ChangedAt = changedAt
        };
    }

    private static MaintenanceReports ConvertToEntity(
        CreateMaintenanceReportDto dto,
        int managerId,
        DateTime createdAt,
        IEnumerable<string> imagePaths)
    {
        return new MaintenanceReports
        {
            ReportedBy = managerId,
            Description = dto.Description.Trim(),
            CreatedAt = createdAt,
            Images = imagePaths
                .Select(path => new MaintenanceReportImages { ImageUrl = path })
                .ToList()
        };
    }

    private void ValidateContractDate(MaintenanceContractReadModel contract)
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

    private static MaintenanceReportDto ConvertToDto(
        MaintenanceReportReadModel report, IReadOnlyList<MaintenanceReportImageDto> images)
    {
        return new MaintenanceReportDto(
            report.ReportId, report.RequestId, report.Description, report.ReportedBy,
            report.ReportedByName, report.CreatedAt, images);
    }

    private static MaintenanceReportImageDto ConvertToDto(
        MaintenanceReportImageReadModel image, string readUrl)
    {
        return new MaintenanceReportImageDto(image.ImageId, readUrl);
    }

    private static MaintenanceRequestDto ConvertToDto(MaintenanceRequestReadModel request)
    {
        return new MaintenanceRequestDto(
            request.RequestId,
            request.Description,
            request.Status,
            request.ContractId,
            request.CustomerId,
            request.CustomerName,
            request.RoomId,
            request.RoomCode,
            request.EquipmentId,
            request.EquipmentName,
            request.EquipmentStatus,
            request.AssignedTo,
            request.AssignedStaffName,
            request.AssignmentNote);
    }

    private static MaintenanceLogDto ConvertToDto(MaintenanceLogReadModel log)
    {
        return new MaintenanceLogDto(
            log.LogId,
            log.RequestId,
            log.OldStatus,
            log.NewStatus,
            log.ChangedBy,
            log.ChangedByName,
            log.ChangedAt);
    }
}

public sealed class MaintenanceAuthorizationException(string message) : Exception(message);
