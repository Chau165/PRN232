import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/app_text_field.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';

class ManagerEquipmentScreen extends StatefulWidget {
  const ManagerEquipmentScreen({super.key});

  @override
  State<ManagerEquipmentScreen> createState() => _ManagerEquipmentScreenState();
}

class _ManagerEquipmentScreenState extends State<ManagerEquipmentScreen> {
  late List<ManagerEquipmentModel> _equipments;
  String _selectedFilter = 'Tất cả';

  final List<String> _filters = ['Tất cả', 'Tốt', 'Cần sửa', 'Hỏng'];

  @override
  void initState() {
    super.initState();
    _equipments = List.from(ManagerMockData.equipments);
  }

  List<ManagerEquipmentModel> get _filteredEquipments {
    if (_selectedFilter != 'Tất cả') {
      return _equipments.where((e) => e.status == _selectedFilter).toList();
    }
    return _equipments;
  }

  void _openAddEquipment() {
    final codeController = TextEditingController();
    final nameController = TextEditingController();
    final roomController = TextEditingController(text: 'P.101');
    final categoryController = TextEditingController(text: 'Điện lạnh');
    String selectedStatus = 'Tốt';

    AppBottomSheet.show(
      context: context,
      title: 'Thêm tài sản / thiết bị',
      subtitle: 'Khai báo thiết bị bàn giao phòng',
      child: StatefulBuilder(
        builder: (ctx, setSheetState) => Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            AppTextField(
              label: 'Mã tài sản (Serial/Code)',
              hint: 'Ví dụ: EQ-AC-202',
              controller: codeController,
            ),
            const SizedBox(height: 12),
            AppTextField(
              label: 'Tên thiết bị',
              hint: 'Ví dụ: Máy lạnh Panasonic Inverter 1.5HP',
              controller: nameController,
            ),
            const SizedBox(height: 12),
            Row(
              children: [
                Expanded(
                  child: AppTextField(
                    label: 'Phòng đặt',
                    hint: 'P.101',
                    controller: roomController,
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: AppTextField(
                    label: 'Loại tài sản',
                    hint: 'Điện lạnh',
                    controller: categoryController,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            Text('Tình trạng hoạt động', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            Row(
              children: ['Tốt', 'Cần sửa', 'Hỏng'].map((st) {
                final isSelected = selectedStatus == st;
                return Padding(
                  padding: const EdgeInsets.only(right: 8),
                  child: ChoiceChip(
                    label: Text(st),
                    selected: isSelected,
                    selectedColor: AppColors.primary,
                    backgroundColor: AppColors.surfaceVariant,
                    labelStyle: TextStyle(
                      color: isSelected ? Colors.white : AppColors.textPrimary,
                    ),
                    onSelected: (selected) {
                      if (selected) {
                        setSheetState(() => selectedStatus = st);
                      }
                    },
                  ),
                );
              }).toList(),
            ),
            const SizedBox(height: 24),
            AppButton(
              text: 'Lưu tài sản mới',
              onPressed: () {
                if (codeController.text.trim().isEmpty || nameController.text.trim().isEmpty) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(content: Text('Vui lòng nhập mã và tên thiết bị.')),
                  );
                  return;
                }

                final newEq = ManagerEquipmentModel(
                  id: DateTime.now().millisecondsSinceEpoch,
                  code: codeController.text.trim().toUpperCase(),
                  name: nameController.text.trim(),
                  roomCode: roomController.text.trim(),
                  category: categoryController.text.trim(),
                  status: selectedStatus,
                  installedDate: 'Hôm nay',
                );

                setState(() {
                  _equipments.insert(0, newEq);
                  ManagerMockData.equipments.insert(0, newEq);
                });

                Navigator.pop(ctx);
                ScaffoldMessenger.of(context).showSnackBar(
                  SnackBar(
                    content: Text('Đã thêm thiết bị "${newEq.name}" vào ${newEq.roomCode}!'),
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

  void _changeStatus(ManagerEquipmentModel eq) {
    showModalBottomSheet(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (ctx) => Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text('Cập nhật trạng thái: ${eq.name}', style: AppTypography.headingSmall),
            const SizedBox(height: 14),
            ListTile(
              title: const Text('Hoạt động Tốt'),
              leading: const Icon(Icons.check_circle_outline, color: AppColors.success),
              onTap: () {
                setState(() => eq.status = 'Tốt');
                Navigator.pop(ctx);
              },
            ),
            ListTile(
              title: const Text('Cần sửa chữa / bảo dưỡng'),
              leading: const Icon(Icons.handyman_outlined, color: AppColors.warning),
              onTap: () {
                setState(() => eq.status = 'Cần sửa');
                Navigator.pop(ctx);
              },
            ),
            ListTile(
              title: const Text('Đã Hỏng / Chờ thay mới'),
              leading: const Icon(Icons.cancel_outlined, color: AppColors.danger),
              onTap: () {
                setState(() => eq.status = 'Hỏng');
                Navigator.pop(ctx);
              },
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Kiểm Kê Tài Sản & Thiết Bị'),
        backgroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Thêm thiết bị mới',
            icon: const Icon(Icons.add_circle_outline_rounded, color: AppColors.primary),
            onPressed: _openAddEquipment,
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
                children: _filters.map((st) {
                  final isSelected = _selectedFilter == st;
                  int count = st == 'Tất cả'
                      ? _equipments.length
                      : _equipments.where((e) => e.status == st).length;

                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text('$st ($count)'),
                      selected: isSelected,
                      selectedColor: AppColors.primary,
                      backgroundColor: AppColors.surfaceVariant,
                      labelStyle: TextStyle(
                        color: isSelected ? Colors.white : AppColors.textPrimary,
                        fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                      ),
                      side: BorderSide(
                        color: isSelected ? AppColors.primary : AppColors.border,
                      ),
                      onSelected: (selected) {
                        if (selected) {
                          setState(() => _selectedFilter = st);
                        }
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
          ),
          const Divider(height: 1),

          // List
          Expanded(
            child: _filteredEquipments.isEmpty
                ? Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.devices_other_rounded, size: 48, color: AppColors.textMuted),
                        const SizedBox(height: 12),
                        Text('Không tìm thấy thiết bị phù hợp',
                            style: AppTypography.bodyMedium.copyWith(color: AppColors.textSecondary)),
                      ],
                    ),
                  )
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _filteredEquipments.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 10),
                    itemBuilder: (context, index) {
                      final eq = _filteredEquipments[index];
                      StatusTone tone = eq.status == 'Tốt'
                          ? StatusTone.green
                          : (eq.status == 'Cần sửa' ? StatusTone.amber : StatusTone.rose);

                      return AppCard(
                        onTap: () => _changeStatus(eq),
                        child: Row(
                          mainAxisAlignment: MainAxisAlignment.spaceBetween,
                          children: [
                            Row(
                              children: [
                                Container(
                                  padding: const EdgeInsets.all(10),
                                  decoration: BoxDecoration(
                                    color: AppColors.surfaceVariant,
                                    borderRadius: BorderRadius.circular(10),
                                  ),
                                  child: const Icon(Icons.devices_rounded,
                                      color: AppColors.primary, size: 22),
                                ),
                                const SizedBox(width: 12),
                                Column(
                                  crossAxisAlignment: CrossAxisAlignment.start,
                                  children: [
                                    Row(
                                      children: [
                                        Container(
                                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                                          decoration: BoxDecoration(
                                            color: AppColors.primaryLight,
                                            borderRadius: BorderRadius.circular(4),
                                          ),
                                          child: Text(eq.roomCode,
                                              style: AppTypography.labelSmall.copyWith(color: AppColors.primary)),
                                        ),
                                        const SizedBox(width: 8),
                                        Text(eq.name, style: AppTypography.labelMedium),
                                      ],
                                    ),
                                    const SizedBox(height: 4),
                                    Text('Mã: ${eq.code} • ${eq.category} • Lắp ngày: ${eq.installedDate}',
                                        style: AppTypography.bodySmall.copyWith(color: AppColors.textMuted)),
                                  ],
                                ),
                              ],
                            ),
                            StatusBadge(label: eq.status, tone: tone),
                          ],
                        ),
                      );
                    },
                  ),
          ),
        ],
      ),
    );
  }
}
