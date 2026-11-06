import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../../../routes/app_routes.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';
import '../widgets/report_damage_bottom_sheet.dart';

class CustomerMyRoomScreen extends StatefulWidget {
  const CustomerMyRoomScreen({super.key});

  @override
  State<CustomerMyRoomScreen> createState() => _CustomerMyRoomScreenState();
}

class _CustomerMyRoomScreenState extends State<CustomerMyRoomScreen> {
  final List<EquipmentItem> _equipments = CustomerMockData.myRoomEquipments;

  IconData _getEquipmentIcon(String name) {
    final lower = name.toLowerCase();
    if (lower.contains('máy lạnh') || lower.contains('điều hòa')) {
      return Icons.ac_unit_rounded;
    } else if (lower.contains('tủ lạnh')) {
      return Icons.kitchen_rounded;
    } else if (lower.contains('nước nóng')) {
      return Icons.water_damage_outlined;
    } else if (lower.contains('giường') || lower.contains('nệm')) {
      return Icons.bed_rounded;
    } else if (lower.contains('quạt')) {
      return Icons.mode_fan_off_rounded;
    }
    return Icons.devices_other_rounded;
  }

  Future<void> _handleReportDamage() async {
    final result = await ReportDamageBottomSheet.show(context);
    if (result == true && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Yêu cầu sửa chữa của bạn đã được gửi tới Quản lý tòa nhà!'),
          backgroundColor: AppColors.primary,
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Phòng Của Tôi'),
        backgroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Hợp đồng thuê',
            icon: const Icon(Icons.description_outlined, color: AppColors.textPrimary),
            onPressed: () => context.push(AppRoutes.customerContracts),
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Current Room Card
            AppCard(
              padding: const EdgeInsets.all(18),
              color: AppColors.primaryLight,
              border: const BorderSide(color: Color(0xFFC7E7E2)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.all(8),
                            decoration: BoxDecoration(
                              color: AppColors.primary,
                              borderRadius: BorderRadius.circular(10),
                            ),
                            child: const Icon(Icons.door_front_door_rounded,
                                color: Colors.white, size: 22),
                          ),
                          const SizedBox(width: 12),
                          Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Phòng 204 • Tầng 2',
                                  style: AppTypography.headingLarge),
                              Text('Nhà trọ Bình An',
                                  style: AppTypography.bodySmall
                                      .copyWith(color: AppColors.textSecondary)),
                            ],
                          ),
                        ],
                      ),
                      const StatusBadge(label: 'Đang thuê'),
                    ],
                  ),
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      const Icon(Icons.location_on_outlined,
                          size: 16, color: AppColors.textMuted),
                      const SizedBox(width: 6),
                      Expanded(
                        child: Text(
                          '12 Đường Số 5, Phường 17, Gò Vấp, TP.HCM',
                          style: AppTypography.bodySmall,
                        ),
                      ),
                    ],
                  ),
                  const Divider(height: 24),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Thời hạn hợp đồng',
                              style: AppTypography.bodySmall),
                          const SizedBox(height: 2),
                          Text('Đến 31/12/2026',
                              style: AppTypography.headingSmall),
                        ],
                      ),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text('Tiền phòng/tháng',
                              style: AppTypography.bodySmall),
                          const SizedBox(height: 2),
                          Text('3.200.000 ₫',
                              style: AppTypography.headingSmall
                                  .copyWith(color: AppColors.primary)),
                        ],
                      ),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 16),

            // Quick Action: Báo hỏng thiết bị
            AppCard(
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: AppColors.warningBg,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Icon(Icons.handyman_outlined,
                        color: AppColors.warning, size: 26),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Sự cố trong phòng?',
                            style: AppTypography.labelLarge),
                        const SizedBox(height: 2),
                        Text('Gửi hình ảnh và báo hỏng thợ tới sửa',
                            style: AppTypography.bodySmall),
                      ],
                    ),
                  ),
                  AppButton(
                    text: 'Báo hỏng',
                    height: 38,
                    variant: AppButtonVariant.primary,
                    icon: const Icon(Icons.add_a_photo_outlined,
                        size: 18, color: Colors.white),
                    onPressed: _handleReportDamage,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Equipment List
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Trang thiết bị bàn giao',
                    style: AppTypography.headingMedium),
                Text('${_equipments.length} món',
                    style: AppTypography.bodySmall
                        .copyWith(color: AppColors.textSecondary)),
              ],
            ),
            const SizedBox(height: 12),
            AppCard(
              padding: EdgeInsets.zero,
              child: ListView.separated(
                physics: const NeverScrollableScrollPhysics(),
                shrinkWrap: true,
                itemCount: _equipments.length,
                separatorBuilder: (context, index) =>
                    const Divider(height: 1, indent: 64),
                itemBuilder: (context, index) {
                  final eq = _equipments[index];
                  final isGood = eq.status.toLowerCase().contains('tốt') ||
                      eq.status.toLowerCase().contains('mới');

                  return ListTile(
                    leading: Container(
                      padding: const EdgeInsets.all(10),
                      decoration: BoxDecoration(
                        color: AppColors.surfaceVariant,
                        borderRadius: BorderRadius.circular(10),
                      ),
                      child: Icon(
                        _getEquipmentIcon(eq.name),
                        color: AppColors.primary,
                        size: 22,
                      ),
                    ),
                    title: Text(eq.name, style: AppTypography.labelMedium),
                    subtitle: Text(
                      'Mã: ${eq.code} • Ngày lắp: ${eq.installedDate}',
                      style: AppTypography.bodySmall
                          .copyWith(color: AppColors.textMuted),
                    ),
                    trailing: StatusBadge(
                      label: eq.status,
                      tone: isGood ? StatusTone.green : StatusTone.amber,
                    ),
                  );
                },
              ),
            ),
            const SizedBox(height: 24),

            // Recent Maintenance History
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Lịch sử sửa chữa gần đây',
                    style: AppTypography.headingMedium),
                TextButton(
                  onPressed: () => context.push(AppRoutes.customerMaintenance),
                  child: Text('Xem tất cả →',
                      style: AppTypography.labelMedium
                          .copyWith(color: AppColors.primary)),
                ),
              ],
            ),
            const SizedBox(height: 8),
            AppCard(
              onTap: () => context.push(AppRoutes.customerMaintenance),
              padding: const EdgeInsets.all(14),
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: const BoxDecoration(
                      color: AppColors.successBg,
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(Icons.check_rounded,
                        color: AppColors.success, size: 20),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Vệ sinh máy lạnh & bơm gas',
                            style: AppTypography.labelLarge),
                        const SizedBox(height: 2),
                        Text('Hoàn tất ngày 15/10/2026 • Thợ Nguyễn Văn Nam',
                            style: AppTypography.bodySmall
                                .copyWith(color: AppColors.textSecondary)),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),
          ],
        ),
      ),
    );
  }
}
