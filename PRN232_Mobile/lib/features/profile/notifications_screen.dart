import 'package:flutter/material.dart';
import '../../core/auth/auth_state.dart';
import '../../core/constants/app_colors.dart';
import '../../core/constants/app_typography.dart';
import '../../core/services/notification_service.dart';
import '../../core/utils/formatters.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/status_badge.dart';

class NotificationItem {
  final int id;
  final String title;
  final String message;
  final String type; // Maintenance, Booking, Viewing, Contract, Invoice, Complaint
  final DateTime createdAt;
  bool isRead;

  NotificationItem({
    required this.id,
    required this.title,
    required this.message,
    required this.type,
    required this.createdAt,
    this.isRead = false,
  });
}

class NotificationsScreen extends StatefulWidget {
  const NotificationsScreen({super.key});

  @override
  State<NotificationsScreen> createState() => _NotificationsScreenState();
}

class _NotificationsScreenState extends State<NotificationsScreen> {
  final NotificationService _notificationService = NotificationService();
  List<NotificationItem> _items = [];

  static final List<NotificationItem> _demoNotifications = [
    NotificationItem(
      id: 1,
      title: 'Hóa đơn tháng 11 đã được phát hành',
      message: 'Hóa đơn tiền phòng và điện nước tháng 11 đã sẵn sàng. Vui lòng thanh toán trước ngày 10/11.',
      type: 'Invoice',
      createdAt: DateTime.now().subtract(const Duration(hours: 2)),
      isRead: false,
    ),
    NotificationItem(
      id: 2,
      title: 'Yêu cầu báo hỏng máy lạnh đã được duyệt',
      message: 'Quản lý đã gán kỹ thuật viên Nguyễn Văn Bình xử lý sửa chữa trong hôm nay.',
      type: 'Maintenance',
      createdAt: DateTime.now().subtract(const Duration(hours: 6)),
      isRead: false,
    ),
    NotificationItem(
      id: 3,
      title: 'Lịch hẹn xem phòng đã được xác nhận',
      message: 'Lịch hẹn xem Phòng 204 vào lúc 15:30 ngày mai đã được quản lý phê duyệt.',
      type: 'Viewing',
      createdAt: DateTime.now().subtract(const Duration(days: 1)),
      isRead: false,
    ),
    NotificationItem(
      id: 4,
      title: 'Đặt cọc thành công phòng Studio 301',
      message: 'Hệ thống đã nhận tiền cọc 3.500.000 ₫ qua VNPay. Hợp đồng đang được khởi tạo.',
      type: 'Booking',
      createdAt: DateTime.now().subtract(const Duration(days: 3)),
      isRead: true,
    ),
    NotificationItem(
      id: 5,
      title: 'Hợp đồng thuê phòng đã kích hoạt',
      message: 'Hợp đồng thuê số #HD-2026-089 đã có hiệu lực chính thức.',
      type: 'Contract',
      createdAt: DateTime.now().subtract(const Duration(days: 7)),
      isRead: true,
    ),
  ];

  @override
  void initState() {
    super.initState();
    _loadNotifications();
  }

  Future<void> _loadNotifications() async {
    try {
      final list = await _notificationService.getNotifications();
      if (mounted) {
        setState(() {
          if (AuthState.isCurrentDemo && list.isEmpty) {
            _items = List.from(_demoNotifications);
          } else {
            _items = list;
          }
        });
      }
    } catch (_) {
      if (mounted && AuthState.isCurrentDemo) {
        setState(() => _items = List.from(_demoNotifications));
      }
    }
  }

  void _markAllRead() {
    setState(() {
      for (var item in _items) {
        item.isRead = true;
      }
    });
    authState.markAllNotificationsRead();
  }

  void _toggleItem(NotificationItem item) {
    setState(() {
      if (!item.isRead) {
        item.isRead = true;
        authState.markNotificationRead();
        _notificationService.markAsRead(item.id);
      }
    });
  }

  IconData _iconForType(String type) {
    switch (type) {
      case 'Invoice':
        return Icons.receipt_long_rounded;
      case 'Maintenance':
        return Icons.build_circle_outlined;
      case 'Viewing':
        return Icons.event_available_rounded;
      case 'Booking':
        return Icons.bookmark_added_rounded;
      case 'Contract':
        return Icons.description_outlined;
      default:
        return Icons.notifications_none_rounded;
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Thông báo hệ thống'),
        actions: [
          TextButton(
            onPressed: _markAllRead,
            child: const Text('Đọc tất cả'),
          ),
        ],
      ),
      body: _items.isEmpty
          ? Center(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  const Icon(Icons.notifications_off_outlined,
                      size: 64, color: AppColors.textMuted),
                  const SizedBox(height: 12),
                  Text('Bạn chưa có thông báo nào.',
                      style: AppTypography.bodyMedium),
                ],
              ),
            )
          : ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: _items.length,
              separatorBuilder: (_, _) => const SizedBox(height: 12),
              itemBuilder: (context, index) {
                final item = _items[index];

                return AppCard(
                  onTap: () => _toggleItem(item),
                  color: item.isRead
                      ? Colors.white
                      : AppColors.primaryLight.withValues(alpha: 0.4),
                  border: BorderSide(
                    color: item.isRead
                        ? AppColors.borderLight
                        : const Color(0xFFC7E7E2),
                    width: 1.2,
                  ),
                  padding: const EdgeInsets.all(14),
                  child: Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: item.isRead
                              ? AppColors.surfaceVariant
                              : AppColors.primaryLight,
                          shape: BoxShape.circle,
                        ),
                        child: Icon(
                          _iconForType(item.type),
                          color: item.isRead
                              ? AppColors.textMuted
                              : AppColors.primary,
                          size: 22,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                Expanded(
                                  child: Text(
                                    item.title,
                                    style: AppTypography.headingSmall.copyWith(
                                      fontWeight: item.isRead
                                          ? FontWeight.w600
                                          : FontWeight.w700,
                                    ),
                                  ),
                                ),
                                if (!item.isRead) ...[
                                  Container(
                                    width: 8,
                                    height: 8,
                                    decoration: const BoxDecoration(
                                      color: AppColors.primary,
                                      shape: BoxShape.circle,
                                    ),
                                  ),
                                ],
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(
                              item.message,
                              style: AppTypography.bodySmall.copyWith(
                                color: item.isRead
                                    ? AppColors.textMuted
                                    : AppColors.textSecondary,
                              ),
                            ),
                            const SizedBox(height: 8),
                            Row(
                              mainAxisAlignment: MainAxisAlignment.spaceBetween,
                              children: [
                                StatusBadge(
                                  label: item.type,
                                  tone: item.isRead
                                      ? StatusTone.slate
                                      : StatusTone.teal,
                                ),
                                Text(
                                  Formatters.formatDateTime(item.createdAt),
                                  style: AppTypography.bodySmall.copyWith(
                                    fontSize: 11,
                                    color: AppColors.textLight,
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                );
              },
            ),
    );
  }
}
