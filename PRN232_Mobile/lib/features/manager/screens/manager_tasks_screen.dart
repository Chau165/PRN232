import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';

class ManagerTasksScreen extends StatefulWidget {
  const ManagerTasksScreen({super.key});

  @override
  State<ManagerTasksScreen> createState() => _ManagerTasksScreenState();
}

class _ManagerTasksScreenState extends State<ManagerTasksScreen> {
  int _currentTab = 0;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          // Sub-Tab Switcher
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            color: Colors.white,
            child: Row(
              children: [
                _buildTabButton(0, 'Hẹn xem (2)'),
                const SizedBox(width: 8),
                _buildTabButton(1, 'Duyệt cọc (1)'),
                const SizedBox(width: 8),
                _buildTabButton(2, 'Bảo trì (2)'),
              ],
            ),
          ),
          const Divider(height: 1),

          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                if (_currentTab == 0) ...[
                  _buildViewingTaskCard(
                    customerName: 'Lê Thu Trang',
                    phone: '0988776655',
                    room: 'Phòng 101 (Studio)',
                    dateTime: '16:30 • Hôm nay (05/11)',
                    note: 'Khách muốn xem ban công và chỗ để xe.',
                  ),
                  const SizedBox(height: 12),
                  _buildViewingTaskCard(
                    customerName: 'Phạm Hoàng Nam',
                    phone: '0977112233',
                    room: 'Phòng 204 (Phòng Đơn)',
                    dateTime: '10:00 • Ngày mai (06/11)',
                    note: 'Muốn xem trước khi đặt cọc.',
                  ),
                ] else if (_currentTab == 1) ...[
                  _buildDepositTaskCard(
                    customerName: 'Nguyễn Văn B',
                    phone: '0909988776',
                    room: 'Phòng 301 (Studio)',
                    amount: '3.500.000 ₫',
                    paymentMethod: 'Chuyển khoản VNPay',
                    time: 'Hôm nay lúc 14:15',
                  ),
                ] else ...[
                  _buildMaintenanceTaskCard(
                    room: 'Phòng 205',
                    device: 'Vòi sen tắm rò rỉ nước',
                    reporter: 'Khách Hoàng Văn Đức',
                    priority: 'Khẩn cấp',
                    time: '2 giờ trước',
                  ),
                  const SizedBox(height: 12),
                  _buildMaintenanceTaskCard(
                    room: 'Phòng 102',
                    device: 'Máy lạnh không lạnh sâu',
                    reporter: 'Khách Trần Minh Quân',
                    priority: 'Bình thường',
                    time: 'Hôm qua',
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTabButton(int index, String label) {
    final isSelected = _currentTab == index;
    return Expanded(
      child: GestureDetector(
        onTap: () => setState(() => _currentTab = index),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 8),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primary : AppColors.surfaceVariant,
            borderRadius: BorderRadius.circular(10),
          ),
          alignment: Alignment.center,
          child: Text(
            label,
            style: AppTypography.labelSmall.copyWith(
              color: isSelected ? Colors.white : AppColors.textSecondary,
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildViewingTaskCard({
    required String customerName,
    required String phone,
    required String room,
    required String dateTime,
    required String note,
  }) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(customerName, style: AppTypography.headingSmall),
              const StatusBadge(label: 'Chờ duyệt', tone: StatusTone.amber),
            ],
          ),
          const SizedBox(height: 4),
          Text('$phone • $room', style: AppTypography.bodySmall),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Row(
              children: [
                const Icon(Icons.access_time_rounded,
                    size: 16, color: AppColors.primary),
                const SizedBox(width: 6),
                Text(dateTime,
                    style: AppTypography.labelMedium
                        .copyWith(color: AppColors.primary)),
              ],
            ),
          ),
          const SizedBox(height: 8),
          Text('Ghi chú: $note', style: AppTypography.bodySmall),
          const Divider(height: 20),
          Row(
            children: [
              Expanded(
                child: AppButton(
                  text: 'Từ chối',
                  height: 38,
                  variant: AppButtonVariant.outline,
                  onPressed: () {},
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: AppButton(
                  text: 'Duyệt lịch hẹn',
                  height: 38,
                  onPressed: () {},
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildDepositTaskCard({
    required String customerName,
    required String phone,
    required String room,
    required String amount,
    required String paymentMethod,
    required String time,
  }) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(customerName, style: AppTypography.headingSmall),
              const StatusBadge(label: 'Chờ xác nhận', tone: StatusTone.teal),
            ],
          ),
          const SizedBox(height: 4),
          Text('$phone • $room', style: AppTypography.bodySmall),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Số tiền cọc:', style: AppTypography.bodySmall),
              Text(amount,
                  style: AppTypography.headingLarge
                      .copyWith(color: AppColors.primary)),
            ],
          ),
          const SizedBox(height: 4),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Phương thức:', style: AppTypography.bodySmall),
              Text('$paymentMethod • $time',
                  style: AppTypography.labelMedium),
            ],
          ),
          const Divider(height: 20),
          Row(
            children: [
              Expanded(
                child: AppButton(
                  text: 'Hủy cọc',
                  height: 38,
                  variant: AppButtonVariant.outline,
                  onPressed: () {},
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: AppButton(
                  text: 'Xác nhận cọc',
                  height: 38,
                  onPressed: () {},
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildMaintenanceTaskCard({
    required String room,
    required String device,
    required String reporter,
    required String priority,
    required String time,
  }) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(room, style: AppTypography.headingSmall),
              StatusBadge(
                label: priority,
                tone: priority == 'Khẩn cấp'
                    ? StatusTone.rose
                    : StatusTone.amber,
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(device, style: AppTypography.labelLarge),
          const SizedBox(height: 4),
          Text('Người báo: $reporter • $time', style: AppTypography.bodySmall),
          const Divider(height: 20),
          Row(
            children: [
              Expanded(
                child: AppButton(
                  text: 'Chi tiết & Gán thợ',
                  height: 38,
                  onPressed: () {},
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
