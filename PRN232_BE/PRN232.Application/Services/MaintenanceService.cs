using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Domain.Entities;

namespace PRN232.Application.Services;

public class MaintenanceService : IMaintenanceService
{
    private readonly IMaintenanceRepository _repository;
    private readonly TimeProvider _timeProvider;

    public MaintenanceService(IMaintenanceRepository repository, TimeProvider timeProvider)
    {
        _repository = repository;
        _timeProvider = timeProvider;
    }

    public Task<List<MaintenanceRequestDto>> GetAllAsync(int? customerId, string? status, int? assignedTo) =>
        _repository.GetAllAsync(customerId, status, assignedTo);

    public Task<MaintenanceRequestDto?> GetByIdAsync(int id) => _repository.GetByIdAsync(id);

    public async Task<MaintenanceRequestDto> CreateAsync(CreateMaintenanceRequestDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Description))
        {
            throw new MaintenanceBusinessRuleException("Description is required.");
        }

        var contract = await GetValidContractAsync(dto.ContractId);
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

        await _repository.AddAsync(request);
        await _repository.SaveChangesAsync();

        return await _repository.GetByIdAsync(request.RequestId)
            ?? throw new InvalidOperationException("Created maintenance request was not found.");
    }

    public async Task<MaintenanceRequestDto?> UpdateAsync(
        int id,
        UpdateMaintenanceRequestDto dto,
        int? actorUserId)
    {
        var request = await _repository.GetStateByIdAsync(id);
        if (request is null)
        {
            return null;
        }

        if (!MaintenanceRequestStatuses.IsAllowedTransition(request.Status, dto.Status))
        {
            throw new MaintenanceStatusTransitionException(
                $"Transition from '{request.Status}' to '{dto.Status}' is not allowed.");
        }

        int? assignedTo = dto.AssignedTo ?? request.AssignedTo;
        if (request.Status == MaintenanceRequestStatuses.Pending &&
            dto.Status == MaintenanceRequestStatuses.Approved)
        {
            if (dto.AssignedTo is null)
            {
                throw new MaintenanceAssigneeException("An assignee is required when approving a request.");
            }

            var contract = await GetValidContractAsync(request.ContractId);
            if (request.EquipmentId is int equipmentId)
            {
                await ValidateEquipmentAsync(equipmentId, contract.RoomId);
            }

            var isActive = await _repository.GetUserActiveStatusAsync(dto.AssignedTo.Value);
            if (isActive is null)
            {
                throw new MaintenanceNotFoundException("Assignee does not exist.");
            }
            if (!isActive.Value)
            {
                throw new MaintenanceAssigneeException("Assignee is inactive.");
            }
        }
        else if (dto.AssignedTo is int explicitAssignee)
        {
            var isActive = await _repository.GetUserActiveStatusAsync(explicitAssignee);
            if (isActive is null)
            {
                throw new MaintenanceNotFoundException("Assignee does not exist.");
            }
            if (!isActive.Value)
            {
                throw new MaintenanceAssigneeException("Assignee is inactive.");
            }
        }

        var changedAt = _timeProvider.GetUtcNow().UtcDateTime;
        var updated = await _repository.UpdateStatusAndAddLogAsync(
            id, request.Status, dto.Status, assignedTo, actorUserId, changedAt);
        if (!updated)
        {
            throw new MaintenanceStatusTransitionException("Request status changed before the update completed.");
        }

        return await _repository.GetByIdAsync(id);
    }

    public Task<List<MaintenanceLogDto>?> GetLogsAsync(int id) => _repository.GetLogsAsync(id);

    private async Task<MaintenanceContractDto> GetValidContractAsync(int contractId)
    {
        var contract = await _repository.GetContractAsync(contractId);
        if (contract is null)
        {
            throw new MaintenanceNotFoundException("Contract does not exist.");
        }

        var today = DateOnly.FromDateTime(_timeProvider.GetUtcNow().UtcDateTime);
        if (today < contract.StartDate || today > contract.EndDate)
        {
            throw new MaintenanceContractException("Contract is outside its valid date range.");
        }

        // Contract status vocabulary is not established in this repository.
        return contract;
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
