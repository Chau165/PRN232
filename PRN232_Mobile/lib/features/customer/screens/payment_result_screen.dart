import 'dart:async';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../routes/app_routes.dart';
import '../models/customer_models.dart';

class PaymentResultScreen extends StatefulWidget {
  final int? requestId;
  final int? bookingId;
  final int? invoiceId;
  final num amount;
  final String? checkoutUrl;
  final bool isBooking;

  const PaymentResultScreen({
    super.key,
    this.requestId,
    this.bookingId,
    this.invoiceId,
    required this.amount,
    this.checkoutUrl,
    this.isBooking = true,
  });

  @override
  State<PaymentResultScreen> createState() => _PaymentResultScreenState();
}

class _PaymentResultScreenState extends State<PaymentResultScreen> with SingleTickerProviderStateMixin {
  final BookingService _bookingService = BookingService();

  PaymentRequestStatusModel? _statusData;
  bool _isChecking = false;
  String? _errorMessage;
  bool _isPaused = false;
  int _pollCount = 0;
  Timer? _pollingTimer;

  late final AnimationController _rotationController;

  static const int _maxPollCount = 30; // tối đa 30 lần (~2 phút)
  static const Duration _pollInterval = Duration(seconds: 4);

  @override
  void initState() {
    super.initState();
    _rotationController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();

    _checkStatus();
    _startPolling();
  }

  @override
  void dispose() {
    _pollingTimer?.cancel();
    _rotationController.dispose();
    super.dispose();
  }

  void _startPolling() {
    _pollingTimer?.cancel();
    _pollingTimer = Timer.periodic(_pollInterval, (timer) {
      if (_statusData?.isSuccess == true || _statusData?.isClosed == true) {
        timer.cancel();
        return;
      }
      if (_pollCount >= _maxPollCount) {
        timer.cancel();
        if (mounted) setState(() => _isPaused = true);
        return;
      }
      _checkStatus(isAuto: true);
    });
  }

  Future<void> _checkStatus({bool isAuto = false}) async {
    final reqId = widget.requestId ?? _statusData?.paymentRequestId;
    if (reqId == null || reqId <= 0) return;

    if (!isAuto) {
      setState(() {
        _isChecking = true;
        _errorMessage = null;
      });
    }

    try {
      final result = await _bookingService.getPaymentRequestStatus(reqId);
      if (!mounted) return;

      setState(() {
        _statusData = result;
        _pollCount++;
        _isChecking = false;
        _errorMessage = null;
      });

      if (result.isSuccess) {
        _pollingTimer?.cancel();
        _rotationController.stop();
      }
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isChecking = false;
        if (!isAuto) {
          _errorMessage = e.toString().replaceFirst('Exception: ', '');
        }
      });
    }
  }

  void _openCheckoutUrl() async {
    final urlStr = widget.checkoutUrl ?? _statusData?.checkoutUrl;
    if (urlStr == null || urlStr.isEmpty) return;

    final uri = Uri.parse(urlStr);
    try {
      final launched = await launchUrl(
        uri,
        mode: LaunchMode.inAppBrowserView,
      );
      if (!launched) {
        await launchUrl(uri, mode: LaunchMode.externalApplication);
      }
    } catch (_) {
      try {
        await launchUrl(uri, mode: LaunchMode.externalApplication);
      } catch (e) {
        if (!mounted) return;
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Không thể mở liên kết payOS: $e'),
            backgroundColor: const Color(0xFFE11D48),
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    final isSuccess = _statusData?.isSuccess ?? false;
    final isClosed = _statusData?.isClosed ?? false;
    final isRequiresReview = _statusData?.status == 'RequiresReview';

    // Nội dung hiển thị
    String title = widget.isBooking
        ? 'Đang chờ xác nhận tiền cọc'
        : 'Đang chờ xác nhận thanh toán';
    String description = widget.isBooking
        ? 'Kết quả được lấy từ hệ thống. Nếu đã chuyển tiền, vui lòng chờ xác nhận, không thanh toán thêm.'
        : (_statusData?.isExpired == true
            ? 'Liên kết đã quá hạn mở checkout. Hệ thống vẫn đang kiểm tra kết quả; nếu đã chuyển tiền, vui lòng không trả thêm.'
            : 'Nếu bạn đã thanh toán, vui lòng chờ hệ thống xác nhận. Không cần thanh toán lại.');

    if (isRequiresReview) {
      title = 'Thanh toán cần kiểm tra';
      description =
          'Vui lòng liên hệ quản lý để kiểm tra khoản thanh toán. Không thanh toán thêm khi chưa có xác nhận.';
    } else if (isSuccess) {
      title = widget.isBooking ? 'Đặt cọc thành công' : 'Thanh toán thành công';
      description = widget.isBooking
          ? 'Hệ thống đã xác nhận nhận cọc qua payOS. Phòng được giữ để lập hợp đồng hoặc xử lý yêu cầu hoàn cọc.'
          : 'Hệ thống đã xác nhận thanh toán hóa đơn qua payOS.';
    } else if (isClosed) {
      title = widget.isBooking ? 'Liên kết cọc đã đóng' : 'Liên kết đã đóng';
      description = widget.isBooking
          ? 'Quay lại booking để kiểm tra trước khi thanh toán tiếp. Nếu đã chuyển tiền, liên hệ quản lý để đối chiếu.'
          : 'Quay lại hóa đơn để kiểm tra và tạo liên kết mới nếu vẫn chưa thanh toán.';
    }

    final hasCheckout = !isSuccess &&
        !isClosed &&
        (widget.checkoutUrl != null || _statusData?.checkoutUrl != null);

    return Scaffold(
      backgroundColor: const Color(0xFFFCFDFD),
      appBar: AppBar(
        title: Text(widget.isBooking ? 'Đặt cọc giữ phòng' : 'Thanh toán hóa đơn'),
        backgroundColor: Colors.white,
        foregroundColor: AppColors.textPrimary,
        elevation: 0,
        centerTitle: true,
      ),
      body: Center(
        child: SingleChildScrollView(
          padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 24),
          child: Container(
            constraints: const BoxConstraints(maxWidth: 440),
            decoration: BoxDecoration(
              color: Colors.white,
              borderRadius: BorderRadius.circular(24),
              boxShadow: [
                BoxShadow(
                  color: const Color(0xFF104143).withOpacity(0.08),
                  blurRadius: 36,
                  offset: const Offset(0, 10),
                ),
              ],
            ),
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 36),
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Icon trạng thái tròn ở giữa
                Center(
                  child: Container(
                    width: 68,
                    height: 68,
                    decoration: BoxDecoration(
                      shape: BoxShape.circle,
                      color: isSuccess
                          ? const Color(0xFFD1FAE5) // Xanh lá nhạt
                          : isClosed
                              ? const Color(0xFFFFE4E6) // Đỏ nhạt
                              : isRequiresReview
                                  ? const Color(0xFFFEF3C7) // Cam nhạt
                                  : const Color(0xFFE6F4F2), // Teal nhạt
                    ),
                    alignment: Alignment.center,
                    child: isSuccess
                        ? const Icon(Icons.check_rounded, color: Color(0xFF059669), size: 38)
                        : isClosed
                            ? const Icon(Icons.close_rounded, color: Color(0xFFE11D48), size: 36)
                            : isRequiresReview
                                ? const Icon(Icons.warning_amber_rounded, color: Color(0xFFD97706), size: 36)
                                : RotationTransition(
                                    turns: _rotationController,
                                    child: const Icon(Icons.refresh_rounded, color: Color(0xFF087775), size: 36),
                                  ),
                  ),
                ),
                const SizedBox(height: 20),

                // Tag danh mục trên cùng
                Center(
                  child: Text(
                    widget.isBooking ? 'ĐẶT CỌC GIỮ PHÒNG' : 'THANH TOÁN HÓA ĐƠN',
                    style: const TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF087775),
                      letterSpacing: 1.2,
                    ),
                  ),
                ),
                const SizedBox(height: 8),

                // Tiêu đề lớn
                Text(
                  title,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF0F172A),
                  ),
                ),
                const SizedBox(height: 12),

                // Mô tả chi tiết
                Text(
                  description,
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontSize: 14,
                    color: Color(0xFF64748B),
                    height: 1.5,
                  ),
                ),
                const SizedBox(height: 16),

                // Thông tin Booking & Tiền cọc
                Center(
                  child: Text.rich(
                    TextSpan(
                      text: widget.isBooking
                          ? 'Booking #${widget.bookingId ?? _statusData?.bookingId ?? ''} · '
                          : 'Hóa đơn #${widget.invoiceId ?? _statusData?.invoiceId ?? ''} · ',
                      style: const TextStyle(fontSize: 15, color: Color(0xFF475569)),
                      children: [
                        TextSpan(
                          text: Formatters.formatCurrency(widget.amount),
                          style: const TextStyle(
                            fontWeight: FontWeight.w800,
                            color: Color(0xFF0F172A),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),

                if (_errorMessage != null) ...[
                  const SizedBox(height: 14),
                  Text(
                    _errorMessage!,
                    textAlign: TextAlign.center,
                    style: const TextStyle(fontSize: 13, color: Color(0xFFE11D48)),
                  ),
                ],

                if (_isPaused && !isSuccess && !isClosed) ...[
                  const SizedBox(height: 14),
                  const Text(
                    'Chưa có kết quả cuối cùng. Đã tạm dừng kiểm tra tự động; bạn có thể bấm kiểm tra lại. Nếu đã chuyển tiền, vui lòng không trả thêm.',
                    textAlign: TextAlign.center,
                    style: TextStyle(fontSize: 12, color: Color(0xFFB45309), height: 1.4),
                  ),
                ],

                const SizedBox(height: 28),

                // Nút 1: Mở checkout payOS (nếu chưa thanh toán thành công)
                if (hasCheckout) ...[
                  ElevatedButton(
                    onPressed: _openCheckoutUrl,
                    style: ElevatedButton.styleFrom(
                      backgroundColor: const Color(0xFF087775),
                      foregroundColor: Colors.white,
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                      elevation: 0,
                    ),
                    child: const Text(
                      'Mở checkout payOS',
                      style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                    ),
                  ),
                  const SizedBox(height: 12),
                ],

                // Nút 2: Kiểm tra lại trạng thái
                if (!isSuccess && !isClosed) ...[
                  OutlinedButton(
                    onPressed: _isChecking
                        ? null
                        : () {
                            _pollCount = 0;
                            _isPaused = false;
                            _checkStatus();
                            _startPolling();
                          },
                    style: OutlinedButton.styleFrom(
                      foregroundColor: const Color(0xFF087775),
                      side: const BorderSide(color: Color(0xFF087775)),
                      padding: const EdgeInsets.symmetric(vertical: 14),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    ),
                    child: _isChecking
                        ? const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(color: Color(0xFF087775), strokeWidth: 2),
                          )
                        : const Text(
                            'Kiểm tra lại trạng thái',
                            style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
                          ),
                  ),
                  const SizedBox(height: 12),
                ],

                // Nút 3: Về lịch xem & đặt cọc / Về hóa đơn của tôi
                ElevatedButton(
                  onPressed: () {
                    if (widget.isBooking) {
                      context.go(AppRoutes.customerBookings);
                    } else {
                      context.go(AppRoutes.customerBills);
                    }
                  },
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFFF1F5F9),
                    foregroundColor: const Color(0xFF475569),
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    elevation: 0,
                  ),
                  child: Text(
                    widget.isBooking ? 'Về lịch xem & đặt cọc' : 'Về hóa đơn của tôi',
                    style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
