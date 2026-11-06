import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/app_text_field.dart';
import '../../../core/widgets/status_badge.dart';
import '../../../routes/app_routes.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';

class ManagerContractsScreen extends StatefulWidget {
  const ManagerContractsScreen({super.key});

  @override
  State<ManagerContractsScreen> createState() => _ManagerContractsScreenState();
}

class _ManagerContractsScreenState extends State<ManagerContractsScreen> {
  int _selectedFilterIndex = 0; // 0: Tất cả, 1: Đang hiệu lực, 2: Sắp hết hạn, 3: Đã thanh lý
  final List<String> _filters = [
    'Tất cả',
    'Đang hiệu lực',
    'Sắp hết hạn',
    'Đã thanh lý',
  ];

  late List<ManagerContractModel> _contracts;

  @override
  void initState() {
    super.initState();
    _contracts = List.from(ManagerMockData.contracts);
  }

  List<ManagerContractModel> get _filteredContracts {
    if (_selectedFilterIndex == 1) {
      return _contracts.where((c) => c.isActive).toList();
    } else if (_selectedFilterIndex == 2) {
      return _contracts.where((c) => c.isExpiringSoon).toList();
    } else if (_selectedFilterIndex == 3) {
      return _contracts.where((c) => c.isTerminated).toList();
    }
    return _contracts;
  }

  // 1. Gia hạn hợp đồng
  void _openExtendContract(ManagerContractModel contract) {
    DateTime newEndDate = contract.endDate.add(const Duration(days: 180)); // +6 tháng
    final rentController = TextEditingController(text: contract.monthlyRent.toString());

    AppBottomSheet.show(
      context: context,
      title: 'Gia hạn hợp đồng',
      subtitle: '${contract.contractCode} • ${contract.roomCode}',
      child: StatefulBuilder(
        builder: (ctx, setSheetState) => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            AppCard(
              color: AppColors.primaryLight,
              border: const BorderSide(color: Color(0xFFC7E7E2)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(contract.primaryTenantName, style: AppTypography.headingSmall),
                  Text('SĐT: ${contract.tenantPhone} • ${contract.roomCode}',
                      style: AppTypography.bodySmall),
                  const Divider(height: 16),
                  Text('Hạn hiện tại: ${Formatters.formatDate(contract.endDate)}',
                      style: AppTypography.bodySmall),
                ],
              ),
            ),
            const SizedBox(height: 16),
            Text('Thời hạn hợp đồng mới', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              onTap: () async {
                final picked = await showDatePicker(
                  context: context,
                  initialDate: newEndDate,
                  firstDate: contract.endDate,
                  lastDate: contract.endDate.add(const Duration(days: 730)),
                );
                if (picked != null) {
                  setSheetState(() => newEndDate = picked);
                }
              },
              child: Row(
                children: [
                  const Icon(Icons.event_repeat_rounded, color: AppColors.primary),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Gia hạn đến ngày', style: AppTypography.bodySmall),
                        Text(Formatters.formatDate(newEndDate), style: AppTypography.labelLarge),
                      ],
                    ),
                  ),
                  const Icon(Icons.arrow_forward_ios_rounded, size: 14, color: AppColors.textMuted),
                ],
              ),
            ),
            const SizedBox(height: 14),
            AppTextField(
              label: 'Giá thuê hàng tháng (VNĐ)',
              hint: 'Nhập giá thuê',
              controller: rentController,
              keyboardType: TextInputType.number,
            ),
            const SizedBox(height: 24),
            AppButton(
              text: 'Xác nhận gia hạn hợp đồng',
              onPressed: () {
                final newRent = num.tryParse(rentController.text) ?? contract.monthlyRent;
                setState(() {
                  contract.endDate = newEndDate;
                  contract.monthlyRent = newRent;
                  contract.status = 'Active';
                });
                Navigator.pop(ctx);
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text('Đã gia hạn hợp đồng ${contract.contractCode} đến ${Formatters.formatDate(newEndDate)}!'),
                    backgroundColor: AppColors.primary,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
              },
            ),
            const SizedBox(height: 10),
          ],
        ),
      ),
    );
  }

  // 2. Thanh lý hợp đồng
  void _openTerminateContract(ManagerContractModel contract) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Thanh lý hợp đồng'),
        content: Text(
          'Bạn có chắc chắn muốn thanh lý hợp đồng ${contract.contractCode} của khách ${contract.primaryTenantName} (Phòng ${contract.roomCode})? Tiền cọc hoàn trả: ${Formatters.formatCurrency(contract.depositAmount)}.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () {
              setState(() {
                contract.status = 'Terminated';
              });
              Navigator.pop(ctx);
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text('Đã thanh lý hợp đồng ${contract.contractCode}. Phòng chuyển sang chờ nghiệm thu bàn giao.'),
                  backgroundColor: AppColors.primary,
                  behavior: SnackBarBehavior.floating,
                ),
              );
            },
            child: const Text('Xác nhận thanh lý', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  // 3. Nghiệm thu phòng sẵn sàng (room-ready)
  void _markRoomReady(ManagerContractModel contract) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Nghiệm thu phòng trống'),
        content: Text(
          'Xác nhận phòng ${contract.roomCode} đã được dọn vệ sinh, kiểm tra đầy đủ tài sản và sẵn sàng đón khách thuê mới?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Chưa xong'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.success),
            onPressed: () {
              // Cập nhật trạng thái phòng trong ManagerMockData.rooms
              try {
                final room = ManagerMockData.rooms.firstWhere((r) => r.roomCode == contract.roomCode);
                room.status = 'Available';
                room.tenants.clear();
              } catch (_) {}

              Navigator.pop(ctx);
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text('Phòng ${contract.roomCode} đã chuyển sang trạng thái "Phòng Trống (Available)"!'),
                  backgroundColor: AppColors.success,
                  behavior: SnackBarBehavior.floating,
                ),
              );
            },
            child: const Text('Phòng sẵn sàng (Room-Ready)', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Quản Lý Hợp Đồng Thuê'),
        backgroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Lập hợp đồng mới',
            icon: const Icon(Icons.note_add_rounded, color: AppColors.primary),
            onPressed: () async {
              final result = await context.push(AppRoutes.managerCreateContract);
              if (result == true) {
                setState(() {
                  _contracts = List.from(ManagerMockData.contracts);
                });
              }
            },
          ),
        ],
      ),
      body: Column(
        children: [
          // Filter Chips
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            color: Colors.white,
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: List.generate(_filters.length, (index) {
                  final isSelected = _selectedFilterIndex == index;
                  int count = 0;
                  if (index == 0) {
                    count = _contracts.length;
                  } else if (index == 1) {
                    count = _contracts.where((c) => c.isActive).length;
                  } else if (index == 2) {
                    count = _contracts.where((c) => c.isExpiringSoon).length;
                  } else if (index == 3) {
                    count = _contracts.where((c) => c.isTerminated).length;
                  }

                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text('${_filters[index]} ($count)'),
                      selected: isSelected,
                      selectedColor: AppColors.primary,
                      backgroundColor: AppColors.surfaceVariant,
                      labelStyle: AppTypography.labelSmall.copyWith(
                        color: isSelected ? Colors.white : AppColors.textPrimary,
                        fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                      ),
                      side: BorderSide(
                        color: isSelected ? AppColors.primary : AppColors.border,
                      ),
                      onSelected: (selected) {
                        if (selected) {
                          setState(() => _selectedFilterIndex = index);
                        }
                      },
                    ),
                  );
                }),
              ),
            ),
          ),
          const Divider(height: 1),

          // Contracts List
          Expanded(
            child: _filteredContracts.isEmpty
                ? Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.description_outlined, size: 48, color: AppColors.textMuted),
                        const SizedBox(height: 12),
                        Text('Không có hợp đồng nào phù hợp bộ lọc',
                            style: AppTypography.bodyMedium.copyWith(color: AppColors.textSecondary)),
                      ],
                    ),
                  )
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _filteredContracts.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      final contract = _filteredContracts[index];
                      return _buildContractItemCard(contract);
                    },
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildContractItemCard(ManagerContractModel contract) {
    StatusTone tone;
    String statusLabel;

    if (contract.isActive) {
      tone = StatusTone.green;
      statusLabel = 'Đang hiệu lực';
    } else if (contract.isExpiringSoon) {
      tone = StatusTone.amber;
      statusLabel = 'Sắp hết hạn';
    } else {
      tone = StatusTone.slate;
      statusLabel = 'Đã thanh lý';
    }

    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      contract.contractCode,
                      style: AppTypography.labelSmall.copyWith(color: AppColors.primary),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text(contract.roomCode, style: AppTypography.headingSmall),
                ],
              ),
              StatusBadge(label: statusLabel, tone: tone),
            ],
          ),
          const SizedBox(height: 6),
          Text(
            'Khách: ${contract.primaryTenantName} (${contract.tenantPhone}) • CCCD: ${contract.tenantCccd}',
            style: AppTypography.bodySmall,
          ),
          const Divider(height: 18),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Thời hạn hợp đồng', style: AppTypography.bodySmall),
                  Text(
                    '${Formatters.formatDate(contract.startDate)} → ${Formatters.formatDate(contract.endDate)}',
                    style: AppTypography.labelLarge,
                  ),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text('Tiền phòng/tháng', style: AppTypography.bodySmall),
                  Text(
                    Formatters.formatCurrency(contract.monthlyRent),
                    style: AppTypography.headingSmall.copyWith(color: AppColors.primary),
                  ),
                ],
              ),
            ],
          ),
          if (contract.notes != null) ...[
            const SizedBox(height: 8),
            Text('Ghi chú: ${contract.notes}',
                style: AppTypography.bodySmall.copyWith(color: AppColors.textSecondary, fontSize: 11)),
          ],
          const Divider(height: 18),
          // Action Buttons
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              if (contract.isTerminated) ...[
                OutlinedButton.icon(
                  icon: const Icon(Icons.cleaning_services_rounded, size: 16),
                  label: const Text('Bàn giao phòng trống (Room-Ready)'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.success,
                    side: const BorderSide(color: AppColors.success),
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: () => _markRoomReady(contract),
                ),
              ] else ...[
                OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.danger,
                    side: const BorderSide(color: AppColors.danger),
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: () => _openTerminateContract(contract),
                  child: const Text('Thanh lý'),
                ),
                const SizedBox(width: 8),
                OutlinedButton(
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.primary,
                    side: const BorderSide(color: AppColors.primary),
                    visualDensity: VisualDensity.compact,
                  ),
                  onPressed: () => _openExtendContract(contract),
                  child: const Text('Gia hạn HĐ'),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}
