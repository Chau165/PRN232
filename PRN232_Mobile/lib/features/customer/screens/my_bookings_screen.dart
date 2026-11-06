import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';

class MyBookingsScreen extends StatefulWidget {
  const MyBookingsScreen({super.key});

  @override
  State<MyBookingsScreen> createState() => _MyBookingsScreenState();
}

class _MyBookingsScreenState extends State<MyBookingsScreen> {
  int _currentTab = 0; // 0: Lịch hẹn xem phòng, 1: Đơn cọc giữ chỗ
  late List<CustomerViewingModel> _viewings;
  late List<CustomerBookingModel> _bookings;

  @override
  void initState() {
    super.initState();
    _viewings = List.from(CustomerMockData.myViewings);
    _bookings = List.from(CustomerMockData.myBookings);
  }

  void _cancelViewing(CustomerViewingModel viewing) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Hủy lịch hẹn xem phòng'),
        content: Text(
            'Bạn có chắc chắn muốn hủy lịch xem phòng ${viewing.roomCode} vào ${viewing.slotTime} không?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Không'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () {
              setState(() {
                viewing.status = 'Cancelled';
              });
              Navigator.pop(ctx);
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text('Đã hủy lịch xem phòng thành công.'),
                  backgroundColor: AppColors.primary,
                  behavior: SnackBarBehavior.floating,
                ),
              );
            },
            child: const Text('Hủy cuộc hẹn', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  void _cancelBooking(CustomerBookingModel booking) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Yêu cầu hủy cọc giữ chỗ'),
        content: Text(
            'Theo quy chế hệ thống, việc hủy cọc giữ chỗ phòng ${booking.roomCode} cần được Quản lý đối soát chính sách hoàn tiền. Bạn có muốn tiếp tục?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Quay lại'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () {
              setState(() {
                booking.status = 'Cancelled';
              });
              Navigator.pop(ctx);
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text('Đã gửi yêu cầu hủy cọc tới Quản lý.'),
                  backgroundColor: AppColors.primary,
                  behavior: SnackBarBehavior.floating,
                ),
              );
            },
            child: const Text('Xác nhận hủy', style: TextStyle(color: Colors.white)),
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
        title: const Text('Lịch Hẹn & Cọc Phòng'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: Column(
        children: [
          // Sub-Tab Switcher
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            color: Colors.white,
            child: Row(
              children: [
                _buildTabButton(0, 'Hẹn xem (${_viewings.length})'),
                const SizedBox(width: 8),
                _buildTabButton(1, 'Đơn cọc phòng (${_bookings.length})'),
              ],
            ),
          ),
          const Divider(height: 1),

          Expanded(
            child: _currentTab == 0
                ? _buildViewingsTab()
                : _buildBookingsTab(),
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
          padding: const EdgeInsets.symmetric(vertical: 9),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primary : AppColors.surfaceVariant,
            borderRadius: BorderRadius.circular(10),
          ),
          alignment: Alignment.center,
          child: Text(
            label,
            style: AppTypography.labelSmall.copyWith(
              color: isSelected ? Colors.white : AppColors.textSecondary,
              fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
            ),
          ),
        ),
      ),
    );
  }

  // TAB 1: LỊCH XEM PHÒNG
  Widget _buildViewingsTab() {
    if (_viewings.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.event_busy_rounded, size: 48, color: AppColors.textMuted),
            const SizedBox(height: 12),
            Text('Bạn chưa có lịch hẹn xem phòng nào',
                style: AppTypography.bodyMedium.copyWith(color: AppColors.textSecondary)),
          ],
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _viewings.length,
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        final viewing = _viewings[index];
        StatusTone tone;
        String statusLabel;

        if (viewing.isConfirmed) {
          tone = StatusTone.green;
          statusLabel = 'Đã xác nhận';
        } else if (viewing.isCancelled) {
          tone = StatusTone.rose;
          statusLabel = 'Đã hủy';
        } else {
          tone = StatusTone.amber;
          statusLabel = 'Chờ duyệt';
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
                          viewing.roomCode,
                          style: AppTypography.labelSmall.copyWith(color: AppColors.primary),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(viewing.propertyName, style: AppTypography.headingSmall),
                    ],
                  ),
                  StatusBadge(label: statusLabel, tone: tone),
                ],
              ),
              const SizedBox(height: 6),
              Row(
                children: [
                  const Icon(Icons.location_on_outlined, size: 14, color: AppColors.textMuted),
                  const SizedBox(width: 4),
                  Expanded(
                    child: Text(viewing.propertyAddress, style: AppTypography.bodySmall),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppColors.primaryLight,
                  borderRadius: BorderRadius.circular(8),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.access_time_rounded, size: 16, color: AppColors.primary),
                    const SizedBox(width: 8),
                    Text(
                      '${Formatters.formatDate(viewing.viewingDate)} • ${viewing.slotTime}',
                      style: AppTypography.labelMedium.copyWith(color: AppColors.primary),
                    ),
                  ],
                ),
              ),
              if (viewing.note != null && viewing.note!.isNotEmpty) ...[
                const SizedBox(height: 8),
                Text('Ghi chú: ${viewing.note}',
                    style: AppTypography.bodySmall.copyWith(color: AppColors.textSecondary)),
              ],
              if (!viewing.isCancelled) ...[
                const Divider(height: 20),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    OutlinedButton(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppColors.danger,
                        side: const BorderSide(color: AppColors.danger),
                        visualDensity: VisualDensity.compact,
                      ),
                      onPressed: () => _cancelViewing(viewing),
                      child: const Text('Hủy lịch hẹn'),
                    ),
                  ],
                ),
              ],
            ],
          ),
        );
      },
    );
  }

  // TAB 2: ĐƠN CỌC PHÒNG
  Widget _buildBookingsTab() {
    if (_bookings.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.bookmark_remove_rounded, size: 48, color: AppColors.textMuted),
            const SizedBox(height: 12),
            Text('Bạn chưa có đơn đặt cọc giữ chỗ nào',
                style: AppTypography.bodyMedium.copyWith(color: AppColors.textSecondary)),
          ],
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _bookings.length,
      separatorBuilder: (_, _) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        final booking = _bookings[index];
        StatusTone tone;
        String statusLabel;

        if (booking.status == 'ContractCreated') {
          tone = StatusTone.green;
          statusLabel = 'Đã lập hợp đồng';
        } else if (booking.isConfirmed) {
          tone = StatusTone.teal;
          statusLabel = 'Đã xác nhận cọc';
        } else if (booking.isCancelled) {
          tone = StatusTone.rose;
          statusLabel = 'Đã hủy cọc';
        } else {
          tone = StatusTone.amber;
          statusLabel = 'Chờ xác nhận';
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
                          booking.roomCode,
                          style: AppTypography.labelSmall.copyWith(color: AppColors.primary),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(booking.roomTypeName, style: AppTypography.headingSmall),
                    ],
                  ),
                  StatusBadge(label: statusLabel, tone: tone),
                ],
              ),
              const SizedBox(height: 4),
              Text('${booking.propertyName} • ${booking.bookingCode}',
                  style: AppTypography.bodySmall),
              const Divider(height: 18),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Tiền cọc giữ chỗ', style: AppTypography.bodySmall),
                      Text(
                        Formatters.formatCurrency(booking.depositAmount),
                        style: AppTypography.headingSmall.copyWith(color: AppColors.primary),
                      ),
                    ],
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text('Ngày dự kiến dọn vào', style: AppTypography.bodySmall),
                      Text(Formatters.formatDate(booking.moveInDate),
                          style: AppTypography.labelLarge),
                    ],
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text(
                'Thanh toán qua: ${booking.paymentMethod} • Ngày cọc: ${Formatters.formatDate(booking.bookingDate)}',
                style: AppTypography.bodySmall.copyWith(color: AppColors.textMuted),
              ),
              if (booking.status != 'ContractCreated' && !booking.isCancelled) ...[
                const Divider(height: 20),
                Row(
                  mainAxisAlignment: MainAxisAlignment.end,
                  children: [
                    OutlinedButton(
                      style: OutlinedButton.styleFrom(
                        foregroundColor: AppColors.danger,
                        side: const BorderSide(color: AppColors.danger),
                        visualDensity: VisualDensity.compact,
                      ),
                      onPressed: () => _cancelBooking(booking),
                      child: const Text('Yêu cầu hủy cọc'),
                    ),
                  ],
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}
