import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';

class CustomerMyRoomScreen extends StatelessWidget {
  const CustomerMyRoomScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Current Room Banner
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
                      Text('Phòng 204 • Tầng 2',
                          style: AppTypography.headingLarge),
                      const StatusBadge(label: 'Đang thuê'),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text('Nhà trọ Bình An • 12 Đường Số 5, Gò Vấp',
                      style: AppTypography.bodySmall),
                  const Divider(height: 24),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Hợp đồng đến:',
                              style: AppTypography.bodySmall),
                          Text('31/12/2026',
                              style: AppTypography.headingSmall),
                        ],
                      ),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.end,
                        children: [
                          Text('Tiền phòng/tháng:',
                              style: AppTypography.bodySmall),
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
            const SizedBox(height: 20),

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
                        color: AppColors.warning, size: 24),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Sự cố thiết bị trong phòng?',
                            style: AppTypography.labelLarge),
                        Text('Gửi ảnh và yêu cầu thợ sửa chữa ngay',
                            style: AppTypography.bodySmall),
                      ],
                    ),
                  ),
                  AppButton(
                    text: 'Báo hỏng',
                    height: 38,
                    variant: AppButtonVariant.primary,
                    onPressed: () {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text('Tính năng Báo Hỏng sẽ hoàn thiện trong Giai đoạn 3!'),
                        ),
                      );
                    },
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Devices list
            Text('Thiết bị trong phòng', style: AppTypography.headingMedium),
            const SizedBox(height: 12),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  _buildDeviceItem('Máy lạnh Panasonic 1.5 HP', 'Hoạt động tốt', Icons.ac_unit_rounded),
                  const Divider(height: 1),
                  _buildDeviceItem('Tủ lạnh Aqua 90L', 'Hoạt động tốt', Icons.kitchen_rounded),
                  const Divider(height: 1),
                  _buildDeviceItem('Máy nước nóng Ariston', 'Hoạt động tốt', Icons.water_damage_outlined),
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildDeviceItem(String name, String status, IconData icon) {
    return ListTile(
      leading: Container(
        padding: const EdgeInsets.all(8),
        decoration: BoxDecoration(
          color: AppColors.surfaceVariant,
          borderRadius: BorderRadius.circular(8),
        ),
        child: Icon(icon, color: AppColors.primary, size: 20),
      ),
      title: Text(name, style: AppTypography.labelMedium),
      trailing: const StatusBadge(label: 'Tốt', tone: StatusTone.green),
    );
  }
}
