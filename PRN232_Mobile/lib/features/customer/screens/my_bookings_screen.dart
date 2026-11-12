import 'package:flutter/material.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/services/viewing_service.dart';
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
  final ViewingService _viewingService = ViewingService();
  final BookingService _bookingService = BookingService();

  int _currentTab = 0; // 0: Lịch hẹn xem phòng, 1: Đơn cọc giữ chỗ
  List<CustomerViewingModel> _viewings = [];
  List<CustomerBookingModel> _bookings = [];
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _viewings = AuthState.isCurrentDemo ? List.from(CustomerMockData.myViewings) : [];
    _bookings = AuthState.isCurrentDemo ? List.from(CustomerMockData.myBookings) : [];
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final vList = await _viewingService.getCustomerViewings();
      final bList = await _bookingService.getBookings();
      if (mounted) {
        setState(() {
          _viewings = vList;
          _bookings = bList;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
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
            onPressed: () async {
              Navigator.pop(ctx);
              setState(() {
                viewing.status = 'Cancelled';
              });
              await _viewingService.rejectViewing(viewing.id, reason: 'Khách tự hủy lịch hẹn');
              if (mounted) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Đã hủy lịch xem phòng thành công.'),
                    backgroundColor: AppColors.primary,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
              }
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
            onPressed: () async {
              Navigator.pop(ctx);
              setState(() {
                booking.status = 'Cancelled';
              });
              await _bookingService.cancelBooking(booking.id);
              if (mounted) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Đã gửi yêu cầu hủy cọc tới Quản lý.'),
                    backgroundColor: AppColors.primary,
                    behavior: SnackBarBehavior.floating,
                  ),
                );
              }
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
            child: _isLoading
                ? const Center(child: CircularProgressIndicator(color: AppColors.primary))
                : RefreshIndicator(
                    onRefresh: _loadData,
                    color: AppColors.primary,
                    child: _currentTab == 0
                        ? _buildViewingsList()
                        : _buildBookingsList(),
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
          padding: const EdgeInsets.symmetric(vertical: 10),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primaryLight : AppColors.surfaceVariant,
            borderRadius: BorderRadius.circular(10),
            border: Border.all(
              color: isSelected ? AppColors.primary : Colors.transparent,
            ),
          ),
          child: Center(
            child: Text(
              label,
              style: AppTypography.labelMedium.copyWith(
                color: isSelected ? AppColors.primary : AppColors.textSecondary,
                fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
              ),
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildViewingsList() {
    if (_viewings.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.event_busy_outlined, size: 64, color: AppColors.textMuted),
            const SizedBox(height: 12),
            Text('Chưa có lịch hẹn xem phòng nào', style: AppTypography.bodyMedium),
          ],
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _viewings.length,
      separatorBuilder: (context, index) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        final item = _viewings[index];
        final isPending = item.isPending;
        final isConfirmed = item.isConfirmed;

        return AppCard(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(item.propertyName, style: AppTypography.headingSmall),
                  StatusBadge(
                    label: isConfirmed
                        ? 'Đã duyệt'
                        : isPending
                            ? 'Chờ duyệt'
                            : 'Đã hủy',
                    tone: isConfirmed
                        ? StatusTone.green
                        : isPending
                            ? StatusTone.amber
                            : StatusTone.slate,
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Row(
                children: [
                  const Icon(Icons.meeting_room_outlined, size: 16, color: AppColors.primary),
                  const SizedBox(width: 6),
                  Text('Phòng ${item.roomCode}', style: AppTypography.labelLarge),
                ],
              ),
              const SizedBox(height: 4),
              Row(
                children: [
                  const Icon(Icons.access_time_rounded, size: 16, color: AppColors.textMuted),
                  const SizedBox(width: 6),
                  Text(
                    '${item.slotTime} • ${Formatters.formatDate(item.viewingDate)}',
                    style: AppTypography.bodySmall.copyWith(color: AppColors.textSecondary),
                  ),
                ],
              ),
              if (item.note != null && item.note!.isNotEmpty) ...[
                const SizedBox(height: 8),
                Container(
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: AppColors.surfaceVariant,
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: Text(
                    'Ghi chú: ${item.note}',
                    style: AppTypography.bodySmall.copyWith(fontStyle: FontStyle.italic),
                  ),
                ),
              ],
              if (isPending || isConfirmed) ...[
                const Divider(height: 20),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton.icon(
                    icon: const Icon(Icons.cancel_outlined, size: 16, color: AppColors.danger),
                    label: const Text('Hủy lịch hẹn', style: TextStyle(color: AppColors.danger)),
                    onPressed: () => _cancelViewing(item),
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }

  Widget _buildBookingsList() {
    if (_bookings.isEmpty) {
      return Center(
        child: Column(
          mainAxisAlignment: MainAxisAlignment.center,
          children: [
            const Icon(Icons.receipt_long_outlined, size: 64, color: AppColors.textMuted),
            const SizedBox(height: 12),
            Text('Chưa có đơn đặt cọc nào', style: AppTypography.bodyMedium),
          ],
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _bookings.length,
      separatorBuilder: (context, index) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        final b = _bookings[index];
        final isConfirmed = b.isConfirmed;

        return AppCard(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(b.bookingCode, style: AppTypography.labelLarge.copyWith(color: AppColors.primary)),
                  StatusBadge(
                    label: isConfirmed ? 'Đã cọc' : (b.isPending ? 'Chờ thanh toán' : 'Đã hủy'),
                    tone: isConfirmed
                        ? StatusTone.green
                        : (b.isPending ? StatusTone.amber : StatusTone.slate),
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Text('${b.roomCode} • ${b.propertyName}', style: AppTypography.headingSmall),
              const SizedBox(height: 8),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text('Tiền cọc giữ chỗ:', style: AppTypography.bodySmall),
                  Text(
                    Formatters.formatCurrency(b.depositAmount),
                    style: AppTypography.headingSmall.copyWith(color: AppColors.primary),
                  ),
                ],
              ),
              const SizedBox(height: 4),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text('Hạn nhận phòng:', style: AppTypography.bodySmall),
                  Text(Formatters.formatDate(b.moveInDate), style: AppTypography.labelMedium),
                ],
              ),
              if (isConfirmed) ...[
                const Divider(height: 20),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton.icon(
                    icon: const Icon(Icons.undo_rounded, size: 16, color: AppColors.danger),
                    label: const Text('Yêu cầu hủy cọc', style: TextStyle(color: AppColors.danger)),
                    onPressed: () => _cancelBooking(b),
                  ),
                ),
              ],
            ],
          ),
        );
      },
    );
  }
}
