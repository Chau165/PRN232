import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/services/viewing_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../../../routes/app_routes.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';
import '../widgets/payos_qr_dialog.dart';

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
  String? _errorMessage;

  @override
  void initState() {
    super.initState();
    _viewings = AuthState.isCurrentDemo ? List.from(CustomerMockData.myViewings) : [];
    _bookings = AuthState.isCurrentDemo ? List.from(CustomerMockData.myBookings) : [];
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() {
      _isLoading = true;
      _errorMessage = null;
    });

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
    } catch (e) {
      if (mounted) {
        final err = e.toString().replaceFirst('Exception: ', '');
        setState(() {
          _isLoading = false;
          _errorMessage = err;
        });
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text(err),
            backgroundColor: AppColors.danger,
          ),
        );
      }
    }
  }

  void _cancelViewing(CustomerViewingModel viewing) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('Hủy lịch hẹn xem phòng'),
        content: Text(
          'Bạn có chắc chắn muốn hủy lịch xem phòng ${viewing.roomCode} tại ${viewing.propertyName} không?',
        ),
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

  void _openPayOS(CustomerBookingModel booking) async {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(
        child: CircularProgressIndicator(color: Color(0xFF087775)),
      ),
    );

    BookingPaymentLinkModel? link;
    String? payError;
    try {
      link = await _bookingService.createPaymentLink(booking.id);
    } catch (e) {
      payError = e.toString().replaceFirst('Exception: ', '');
    }

    if (!mounted) return;
    Navigator.of(context).pop();

    if (link == null || link.checkoutUrl == null || link.checkoutUrl!.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(payError ?? 'Không thể tạo liên kết thanh toán payOS.'),
          backgroundColor: const Color(0xFFE11D48),
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }

    // 1. Mở trang checkout payOS thật với mã VietQR PRO thật
    final checkoutUri = Uri.parse(link.checkoutUrl!);
    try {
      final launched = await launchUrl(
        checkoutUri,
        mode: LaunchMode.inAppBrowserView,
      );
      if (!launched) {
        await launchUrl(checkoutUri, mode: LaunchMode.externalApplication);
      }
    } catch (_) {
      try {
        await launchUrl(checkoutUri, mode: LaunchMode.externalApplication);
      } catch (_) {}
    }

    if (!mounted) return;

    // 2. Chuyển sang màn hình "Đang chờ xác nhận tiền cọc" (giống ảnh Web)
    context.push(
      AppRoutes.paymentResult,
      extra: {
        'requestId': link.paymentRequestId,
        'bookingId': booking.id,
        'amount': booking.depositAmount,
        'checkoutUrl': link.checkoutUrl,
        'isBooking': true,
      },
    );
  }

  void _checkTransaction(CustomerBookingModel booking) async {
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(
        child: CircularProgressIndicator(color: Color(0xFF087775)),
      ),
    );

    try {
      final reqId = booking.latestPaymentRequestId ?? (booking.id * 3 + 7);
      final status = await _bookingService.getPaymentRequestStatus(reqId);
      if (!mounted) return;
      Navigator.of(context).pop();

      final statusStr = status.status;
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Trạng thái giao dịch #$reqId: $statusStr'),
          backgroundColor: statusStr == 'Succeeded' ? const Color(0xFF087775) : const Color(0xFFD97706),
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (_) {
      if (!mounted) return;
      Navigator.of(context).pop();
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Chưa thể lấy thông tin giao dịch.'),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }
  void _cancelBooking(CustomerBookingModel booking) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
        title: const Text('Yêu cầu hủy cọc giữ chỗ'),
        content: Text(
          'Theo quy chế hệ thống, việc hủy cọc giữ chỗ phòng ${booking.roomCode} cần được Quản lý đối soát chính sách hoàn tiền. Bạn có muốn tiếp tục?',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Đóng'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () async {
              Navigator.pop(ctx);
              try {
                await _bookingService.cancelBooking(booking.id);
                setState(() {
                  booking.status = 'Cancelled';
                });
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    const SnackBar(
                      content: Text('Đã gửi yêu cầu hủy giữ phòng.'),
                      backgroundColor: AppColors.primary,
                      behavior: SnackBarBehavior.floating,
                    ),
                  );
                }
              } catch (e) {
                if (mounted) {
                  ScaffoldMessenger.of(context).showSnackBar(
                    SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
                  );
                }
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
        title: const Text('Lịch Xem & Đặt Cọc'),
        backgroundColor: Colors.white,
        foregroundColor: AppColors.textPrimary,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios_new_rounded, color: AppColors.textPrimary),
          onPressed: () {
            if (Navigator.of(context).canPop()) {
              Navigator.of(context).pop();
            } else {
              context.go(AppRoutes.customerInitial);
            }
          },
        ),
      ),
      body: Column(
        children: [
          // Sub-Tab Switcher
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            color: Colors.white,
            child: Row(
              children: [
                _buildTabButton(0, 'Lịch xem phòng (${_viewings.length})'),
                const SizedBox(width: 8),
                _buildTabButton(1, 'Đặt cọc giữ phòng (${_bookings.length})'),
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
    if (_errorMessage != null && _viewings.isEmpty) {
      return Center(
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline_rounded, size: 64, color: AppColors.danger),
                const SizedBox(height: 12),
                Text('Không thể tải lịch hẹn', style: AppTypography.headingSmall),
                const SizedBox(height: 6),
                Text(
                  _errorMessage!,
                  style: AppTypography.bodySmall.copyWith(color: AppColors.danger),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                ElevatedButton.icon(
                  onPressed: _loadData,
                  icon: const Icon(Icons.refresh_rounded, size: 18),
                  label: const Text('Tải lại'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
    }

    if (_viewings.isEmpty) {
      return Center(
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.event_busy_outlined, size: 64, color: AppColors.textMuted),
                const SizedBox(height: 12),
                Text('Bạn chưa đặt lịch xem phòng nào.', style: AppTypography.headingSmall),
                const SizedBox(height: 6),
                Text(
                  'Khi tìm được phòng trọ ưng ý, hãy nhấn nút "Hẹn xem phòng" để đăng ký thời gian xem phòng trực tiếp.',
                  style: AppTypography.bodySmall,
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
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

        // Map status label & tone chuẩn như Web
        String statusLabel = 'Chờ xác nhận';
        StatusTone statusTone = StatusTone.amber;

        if (item.status == 'Approved' || item.status == 'Confirmed') {
          statusLabel = 'Đã duyệt';
          statusTone = StatusTone.green;
        } else if (item.status == 'Rejected') {
          statusLabel = 'Bị từ chối';
          statusTone = StatusTone.rose;
        } else if (item.status == 'Cancelled') {
          statusLabel = 'Đã huỷ';
          statusTone = StatusTone.slate;
        }

        return AppCard(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Text(
                      '${item.propertyName} · Phòng ${item.roomCode}',
                      style: AppTypography.headingSmall,
                    ),
                  ),
                  const SizedBox(width: 8),
                  StatusBadge(
                    label: statusLabel,
                    tone: statusTone,
                  ),
                ],
              ),
              const SizedBox(height: 6),
              Row(
                children: [
                  const Icon(Icons.access_time_rounded, size: 16, color: AppColors.textMuted),
                  const SizedBox(width: 6),
                  Text(
                    'Thời gian: ${item.slotTime} ${Formatters.formatDate(item.viewingDate)}',
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
    if (_errorMessage != null && _bookings.isEmpty) {
      return Center(
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.error_outline_rounded, size: 64, color: AppColors.danger),
                const SizedBox(height: 12),
                Text('Không thể tải đơn cọc phòng', style: AppTypography.headingSmall),
                const SizedBox(height: 6),
                Text(
                  _errorMessage!,
                  style: AppTypography.bodySmall.copyWith(color: AppColors.danger),
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                ElevatedButton.icon(
                  onPressed: _loadData,
                  icon: const Icon(Icons.refresh_rounded, size: 18),
                  label: const Text('Tải lại'),
                  style: ElevatedButton.styleFrom(
                    backgroundColor: AppColors.primary,
                    foregroundColor: Colors.white,
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
    }

    if (_bookings.isEmpty) {
      return Center(
        child: SingleChildScrollView(
          physics: const AlwaysScrollableScrollPhysics(),
          child: Padding(
            padding: const EdgeInsets.all(24),
            child: Column(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.receipt_long_outlined, size: 64, color: AppColors.textMuted),
                const SizedBox(height: 12),
                Text('Bạn chưa đặt cọc phòng nào.', style: AppTypography.headingSmall),
                const SizedBox(height: 6),
                Text(
                  'Khi chọn được phòng trọ ưng ý, bạn có thể đặt cọc giữ chỗ trực tuyến để đảm bảo phòng không bị người khác thuê mất.',
                  style: AppTypography.bodySmall,
                  textAlign: TextAlign.center,
                ),
              ],
            ),
          ),
        ),
      );
    }

    return ListView.separated(
      padding: const EdgeInsets.all(16),
      itemCount: _bookings.length,
      separatorBuilder: (context, index) => const SizedBox(height: 12),
      itemBuilder: (context, index) {
        final b = _bookings[index];
        return _buildBookingCard(b);
      },
    );
  }

  Widget _buildBookingCard(CustomerBookingModel b) {
    final isConfirmed = b.isConfirmed;
    final isExpired = b.isExpired;
    final hasContract = b.contractId != null;

    // Map status label & tone chuẩn như Web
    String statusLabel = 'Chờ thanh toán';
    StatusTone statusTone = StatusTone.amber;

    if (hasContract) {
      statusLabel = 'Đã lập hợp đồng';
      statusTone = StatusTone.green;
    } else if (b.status == 'Deposited' || isConfirmed) {
      statusLabel = 'Đã đặt cọc';
      statusTone = StatusTone.teal;
    } else if (b.isCancelled) {
      statusLabel = 'Đã huỷ';
      statusTone = StatusTone.rose;
    } else if (isExpired) {
      statusLabel = 'Hết hạn thanh toán';
      statusTone = StatusTone.slate;
    }

    final expiryDateFormatted = b.expiryDate != null
        ? Formatters.formatDateTime(b.expiryDate)
        : '15 phút kể từ lúc đặt';

    final isWaitingPayment = b.status == 'Pending' && !isExpired;

    return Container(
      key: ValueKey('booking_${b.id}_${b.status}'),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
        boxShadow: [
          BoxShadow(
            color: Colors.black.withOpacity(0.02),
            blurRadius: 8,
            offset: const Offset(0, 2),
          ),
        ],
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          // 1. Mã đơn & Trạng thái
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              Text(
                b.bookingCode.toUpperCase(),
                style: const TextStyle(
                  color: Color(0xFF087775),
                  fontWeight: FontWeight.w700,
                  fontSize: 14,
                  letterSpacing: 0.5,
                ),
              ),
              StatusBadge(
                label: statusLabel,
                tone: statusTone,
              ),
            ],
          ),
          const SizedBox(height: 8),

          // 2. Tên khu trọ
          Text(
            b.propertyName,
            style: const TextStyle(
              fontSize: 18,
              fontWeight: FontWeight.w800,
              color: Color(0xFF0F172A),
            ),
          ),
          const SizedBox(height: 4),

          // 3. Số phòng
          Text(
            'Phòng ${b.roomCode}',
            style: const TextStyle(
              fontSize: 13,
              color: Color(0xFF64748B),
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: 2),

          // 4. Ngày dọn vào & Số người ở
          Text(
            'Dọn vào ${Formatters.formatDate(b.moveInDate)} · ${b.occupantCount} người',
            style: const TextStyle(
              fontSize: 13,
              color: Color(0xFF64748B),
            ),
          ),
          const SizedBox(height: 14),
          const Divider(height: 1, color: Color(0xFFF1F5F9)),
          const SizedBox(height: 14),

          // 5. Tiền cọc & Hạn thanh toán
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Tiền cọc',
                    style: TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    Formatters.formatCurrency(b.depositAmount),
                    style: const TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF087775),
                    ),
                  ),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  const Text(
                    'Hạn thanh toán cọc',
                    style: TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    expiryDateFormatted,
                    style: const TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 10),

          // 6. Ghi chú chính sách cọc
          const Text(
            'Tiền cọc được trừ vào tiền phòng tháng đầu tiên khi lập hợp đồng.',
            style: TextStyle(fontSize: 12, color: Color(0xFF64748B), height: 1.4),
          ),

          // 7. Nhóm nút tác vụ khi trạng thái là Chờ thanh toán và chưa hết hạn
          if (isWaitingPayment) ...[
            const SizedBox(height: 16),
            Row(
              children: [
                Expanded(
                  child: InkWell(
                    onTap: () => _openPayOS(b),
                    borderRadius: BorderRadius.circular(8),
                    child: Container(
                      padding: const EdgeInsets.symmetric(vertical: 11),
                      decoration: BoxDecoration(
                        color: const Color(0xFF087775),
                        borderRadius: BorderRadius.circular(8),
                      ),
                      alignment: Alignment.center,
                      child: const Text(
                        'Thanh toán cọc',
                        style: TextStyle(
                          color: Colors.white,
                          fontSize: 13,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                InkWell(
                  onTap: () => _cancelBooking(b),
                  borderRadius: BorderRadius.circular(8),
                  child: Container(
                    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFFF1F2),
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(color: const Color(0xFFFECDD3)),
                    ),
                    alignment: Alignment.center,
                    child: const Text(
                      'Huỷ giữ phòng',
                      style: TextStyle(
                        color: Color(0xFFE11D48),
                        fontSize: 13,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 12),
            InkWell(
              onTap: () => _checkTransaction(b),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 2),
                child: Text(
                  'Kiểm tra giao dịch cọc #${b.latestPaymentRequestId ?? (b.id * 3 + 7)}',
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w600,
                    color: Color(0xFF087775),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }
}
