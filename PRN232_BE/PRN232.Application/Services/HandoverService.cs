using System.Globalization;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.ReadModels;
using PRN232.Application.States;

namespace PRN232.Application.Services;

// Rebuild 3.5 (PLAN.md D3, D6): biên bản bàn giao thiết bị lúc nhận phòng và trả phòng.
// - Ai xem: khách của hợp đồng, quản lý đang phụ trách khu trọ, Admin. Ai ghi: chỉ quản lý đang phụ trách
//   (giống mọi thao tác sửa hợp đồng).
// - Bồi thường (BLDS 2015 Điều 479): chỉ khi trả phòng, chỉ thiết bị Hư hỏng/Mất, tối đa giá trị đã chụp lại
//   × số lượng; thiết bị đã hỏng/mất từ lúc nhận phòng thì không tính tiền khách; hao mòn không bồi thường.
public sealed class HandoverService(
    IHandoverRepository repository,
    TimeProvider clock,
    IDepositSettlementRepository? settlements = null) : IHandoverService
{
    private static readonly CultureInfo Vietnamese = CultureInfo.GetCultureInfo("vi-VN");

    public async Task<ContractHandoversDto> GetAsync(int contractId, int userId, string role, CancellationToken cancellationToken)
    {
        var contract = await GetContractAsync(contractId, cancellationToken);
        var scope = AccessScope.For(userId, role)
            ?? throw new ContractAuthorizationException("Your role cannot view handover records.");
        if (scope.CustomerId is { } customerId && contract.CustomerId != customerId)
        {
            throw new ContractAuthorizationException("You cannot view another customer's handover records.");
        }
        if (scope.ManagerId is { } managerId &&
            !await repository.HasActiveManagerAssignmentAsync(contract.PropertyId, managerId, cancellationToken))
        {
            throw new ContractAuthorizationException("You are not assigned to manage this property's contracts.");
        }
        return await BuildAsync(contract, cancellationToken);
    }

    public async Task<ContractHandoversDto> SaveAsync(int contractId, string kind, SaveHandoverDto dto, int userId,
        string role, CancellationToken cancellationToken)
    {
        if (role != RoleNames.Manager)
        {
            throw new ContractAuthorizationException("Only an assigned Manager can record a handover.");
        }
        var contract = await GetContractAsync(contractId, cancellationToken);
        if (!await repository.HasActiveManagerAssignmentAsync(contract.PropertyId, userId, cancellationToken))
        {
            throw new ContractAuthorizationException("You are not assigned to manage this property's contracts.");
        }

        var records = await repository.GetRecordsAsync(contractId, cancellationToken);
        var moveIn = records.SingleOrDefault(record => record.Kind == HandoverKinds.MoveIn);
        var moveOut = records.SingleOrDefault(record => record.Kind == HandoverKinds.MoveOut);
        if (kind == HandoverKinds.MoveIn)
        {
            if (contract.Status != ContractStatuses.Active)
            {
                throw new ContractConflictException("Chỉ hợp đồng đang hiệu lực mới lập được biên bản nhận phòng.");
            }
            if (moveOut is not null)
            {
                throw new ContractConflictException("Đã có biên bản trả phòng nên không sửa được biên bản nhận phòng.");
            }
        }
        else if (contract.Status is not (ContractStatuses.Active or ContractStatuses.Terminated))
        {
            throw new ContractConflictException("Hợp đồng này không lập được biên bản trả phòng.");
        }
        else if (settlements is not null && await settlements.ExistsAsync(contractId, cancellationToken))
        {
            // Rebuild 3.6b: tiền bồi thường đã được trừ vào cọc theo biên bản này.
            throw new ContractConflictException("Đã quyết toán cọc nên không sửa được biên bản trả phòng.");
        }

        if (dto.HandoverDate < contract.StartDate)
        {
            throw new ContractBusinessRuleException("Ngày bàn giao không được trước ngày bắt đầu hợp đồng.");
        }
        if (kind == HandoverKinds.MoveOut && moveIn is not null && dto.HandoverDate < moveIn.HandoverDate)
        {
            throw new ContractBusinessRuleException("Ngày trả phòng không được trước ngày nhận phòng.");
        }

        var equipments = await repository.GetRoomEquipmentsAsync(contract.RoomId, cancellationToken);
        var items = BuildItems(kind, dto.Items, equipments, moveIn);
        var note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();
        if (!await repository.SaveAsync(contractId, kind, dto.HandoverDate, note, userId,
                clock.GetUtcNow().UtcDateTime, items, cancellationToken))
        {
            throw new ContractConflictException("Biên bản vừa được người khác cập nhật. Vui lòng tải lại.");
        }
        return await BuildAsync(contract, cancellationToken);
    }

    private static List<HandoverItemWriteModel> BuildItems(string kind, IReadOnlyList<SaveHandoverItemDto> lines,
        IReadOnlyList<HandoverEquipmentReadModel> equipments, HandoverRecordReadModel? moveIn)
    {
        var missing = equipments.Where(equipment => lines.All(line => line.EquipmentId != equipment.EquipmentId)).ToList();
        if (missing.Count > 0)
        {
            throw new ContractBusinessRuleException(
                $"Biên bản phải ghi đủ thiết bị của phòng, còn thiếu: {string.Join(", ", missing.Select(item => item.Name))}.");
        }

        var result = new List<HandoverItemWriteModel>();
        for (var index = 0; index < lines.Count; index++)
        {
            var line = lines[index];
            var equipment = equipments.SingleOrDefault(item => item.EquipmentId == line.EquipmentId)
                ?? throw new HandoverItemException(index, "equipmentId", "Thiết bị này không thuộc phòng của hợp đồng.");
            var charge = line.ChargeAmount ?? 0m;
            // Trả phòng: dùng số lượng và giá trị đã chụp lúc nhận phòng (mức hai bên đã thống nhất).
            var agreed = moveIn?.Items.FirstOrDefault(item => item.EquipmentId == equipment.EquipmentId);
            var quantity = agreed?.Quantity ?? equipment.Quantity;
            var value = agreed?.CompensationValue ?? equipment.CompensationValue;

            if (kind == HandoverKinds.MoveIn && charge > 0m)
            {
                throw new HandoverItemException(index, "chargeAmount", "Biên bản nhận phòng không tính tiền bồi thường.");
            }
            if (kind == HandoverKinds.MoveOut && charge > 0m)
            {
                if (!EquipmentConditions.IsChargeable(line.Condition))
                {
                    throw new HandoverItemException(index, "chargeAmount",
                        $"{equipment.Name}: chỉ tính tiền khi thiết bị hư hỏng hoặc mất (hao mòn tự nhiên không phải bồi thường).");
                }
                if (EquipmentConditions.IsChargeable(agreed?.Condition))
                {
                    throw new HandoverItemException(index, "chargeAmount",
                        $"{equipment.Name} đã hư hỏng hoặc mất từ lúc nhận phòng nên không tính tiền khách.");
                }
                if (charge > value * quantity)
                {
                    throw new HandoverItemException(index, "chargeAmount",
                        $"{equipment.Name}: tiền bồi thường tối đa {Money(value * quantity)} ({quantity} × {Money(value)}).");
                }
            }

            result.Add(new HandoverItemWriteModel(equipment.EquipmentId, equipment.Name, quantity, value,
                line.Condition, charge, string.IsNullOrWhiteSpace(line.Note) ? null : line.Note.Trim()));
        }
        return result;
    }

    private async Task<HandoverContractReadModel> GetContractAsync(int contractId, CancellationToken cancellationToken) =>
        await repository.GetContractAsync(contractId, cancellationToken)
            ?? throw new ContractNotFoundException("Contract does not exist.");

    private async Task<ContractHandoversDto> BuildAsync(HandoverContractReadModel contract, CancellationToken cancellationToken)
    {
        var records = await repository.GetRecordsAsync(contract.ContractId, cancellationToken);
        var equipments = await repository.GetRoomEquipmentsAsync(contract.RoomId, cancellationToken);
        var moveIn = records.SingleOrDefault(record => record.Kind == HandoverKinds.MoveIn);
        var moveOut = records.SingleOrDefault(record => record.Kind == HandoverKinds.MoveOut);
        return new ContractHandoversDto(
            contract.ContractId,
            moveIn is null ? null : ToDto(moveIn, null),
            moveOut is null ? null : ToDto(moveOut, moveIn),
            equipments.Select(item => new HandoverEquipmentDto(item.EquipmentId, item.Name, item.Quantity, item.CompensationValue)).ToList());
    }

    private static HandoverDto ToDto(HandoverRecordReadModel record, HandoverRecordReadModel? moveIn) => new(
        record.HandoverRecordId,
        record.Kind,
        record.HandoverDate,
        record.Note,
        record.RecordedByName,
        UtcTimestamp.ForResponse(record.RecordedAtUtc),
        record.Items.Sum(item => item.ChargeAmount),
        record.Items.Select(item => new HandoverItemDto(item.HandoverItemId, item.EquipmentId, item.EquipmentName,
            item.Quantity, item.CompensationValue, item.Condition, item.ChargeAmount, item.Note,
            moveIn?.Items.FirstOrDefault(previous => previous.EquipmentId == item.EquipmentId && item.EquipmentId != null)?.Condition))
            .ToList());

    private static string Money(decimal amount) => $"{amount.ToString("#,0", Vietnamese)}đ";
}

// Lỗi gắn với một dòng thiết bị: controller trả 400 với key "items[{Index}].{Field}" để FE hiện đúng dòng.
public sealed class HandoverItemException(int index, string field, string message) : Exception(message)
{
    public string FieldKey { get; } = $"items[{index}].{field}";
}
