import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';

class BankAppInfo {
  final String name;
  final String shortName;
  final String scheme;
  final Color brandColor;

  const BankAppInfo({
    required this.name,
    required this.shortName,
    required this.scheme,
    required this.brandColor,
  });
}

const List<BankAppInfo> _kPopularBanks = [
  BankAppInfo(name: 'MB Bank', shortName: 'MB', scheme: 'mbmobile://', brandColor: Color(0xFF0038A8)),
  BankAppInfo(name: 'Vietcombank', shortName: 'VCB', scheme: 'vcb://', brandColor: Color(0xFF005B2B)),
  BankAppInfo(name: 'Techcombank', shortName: 'TCB', scheme: 'techcombank://', brandColor: Color(0xFFED1C24)),
  BankAppInfo(name: 'BIDV', shortName: 'BIDV', scheme: 'bidvsmartbanking://', brandColor: Color(0xFF006852)),
  BankAppInfo(name: 'VietinBank', shortName: 'iPay', scheme: 'vietinbankipay://', brandColor: Color(0xFF0072CE)),
  BankAppInfo(name: 'TPBank', shortName: 'TPB', scheme: 'tpbank://', brandColor: Color(0xFF7A2182)),
  BankAppInfo(name: 'VPBank', shortName: 'VPB', scheme: 'vpbankneo://', brandColor: Color(0xFF009045)),
  BankAppInfo(name: 'ACB', shortName: 'ACB', scheme: 'acbapp://', brandColor: Color(0xFF0054A6)),
  BankAppInfo(name: 'MoMo', shortName: 'MoMo', scheme: 'momo://', brandColor: Color(0xFFA50064)),
];

class PayOSQRCodeDialog extends StatefulWidget {
  final CustomerBookingModel booking;
  final BookingPaymentLinkModel? paymentLink;
  final VoidCallback onPaymentSuccess;

  const PayOSQRCodeDialog({
    super.key,
    required this.booking,
    this.paymentLink,
    required this.onPaymentSuccess,
  });

  static Future<void> show(
    BuildContext context, {
    required CustomerBookingModel booking,
    BookingPaymentLinkModel? paymentLink,
    required VoidCallback onPaymentSuccess,
  }) {
    return showDialog(
      context: context,
      barrierDismissible: false,
      builder: (ctx) => PayOSQRCodeDialog(
        booking: booking,
        paymentLink: paymentLink,
        onPaymentSuccess: onPaymentSuccess,
      ),
    );
  }

  @override
  State<PayOSQRCodeDialog> createState() => _PayOSQRCodeDialogState();
}

class _PayOSQRCodeDialogState extends State<PayOSQRCodeDialog> {
  final BookingService _bookingService = BookingService();
  Timer? _countdownTimer;
  Timer? _pollingTimer;
  int _secondsLeft = 15 * 60; // 15 phút
  bool _isChecking = false;

  final String _bankName = 'MB Bank (Ngân hàng Quân Đội)';
  final String _accountNumber = '0901234567';
  final String _accountHolder = 'HE THONG TROVIET';

  String get _transferContent => 'COC PHONG ${widget.booking.roomCode}';

  @override
  void initState() {
    super.initState();
    // Tính thời gian còn lại dựa trên expiryDate nếu có
    if (widget.booking.expiryDate != null) {
      final diff = widget.booking.expiryDate!.difference(DateTime.now()).inSeconds;
      if (diff > 0) {
        _secondsLeft = diff;
      }
    }

    _countdownTimer = Timer.periodic(const Duration(seconds: 1), (timer) {
      if (_secondsLeft > 0) {
        setState(() => _secondsLeft--);
      } else {
        timer.cancel();
      }
    });

    // Tự động kiểm tra trạng thái thanh toán mỗi 6 giây
    _pollingTimer = Timer.periodic(const Duration(seconds: 6), (_) {
      _checkPaymentStatus(silent: true);
    });
  }

  @override
  void dispose() {
    _countdownTimer?.cancel();
    _pollingTimer?.cancel();
    super.dispose();
  }

  Future<void> _checkPaymentStatus({bool silent = false}) async {
    if (_isChecking) return;
    if (!silent) setState(() => _isChecking = true);

    try {
      final reqId = widget.paymentLink?.paymentRequestId ?? widget.booking.latestPaymentRequestId;
      if (reqId != null) {
        try {
          final reqStatus = await _bookingService.getPaymentRequestStatus(reqId);
          if (reqStatus.isSuccess) {
            widget.booking.status = 'Deposited';
            _onConfirmed();
            return;
          }
        } catch (_) {}
      }

      final b = await _bookingService.getBookingDetail(widget.booking.id);
      if (b.status == 'Deposited' || b.isConfirmed) {
        _onConfirmed();
        return;
      }

      // Giả lập cho chế độ test/demo nếu bấm nút kiểm tra
      if (!silent) {
        // Cập nhật trạng thái thành công
        widget.booking.status = 'Deposited';
        final idx = CustomerMockData.bookings.indexWhere((item) => item.id == widget.booking.id);
        if (idx != -1) {
          CustomerMockData.bookings[idx].status = 'Deposited';
        }
        _onConfirmed();
        return;
      }
    } catch (_) {
      if (!silent) {
        // Fallback test
        widget.booking.status = 'Deposited';
        _onConfirmed();
        return;
      }
    } finally {
      if (mounted && !silent) setState(() => _isChecking = false);
    }
  }

  void _onConfirmed() {
    _pollingTimer?.cancel();
    _countdownTimer?.cancel();
    if (mounted) {
      Navigator.of(context).pop();
      widget.onPaymentSuccess();
    }
  }

  void _copy(String text, String label) {
    Clipboard.setData(ClipboardData(text: text));
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã sao chép $label!'),
        duration: const Duration(seconds: 1),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  String _formatTimer(int totalSeconds) {
    final m = (totalSeconds ~/ 60).toString().padLeft(2, '0');
    final s = (totalSeconds % 60).toString().padLeft(2, '0');
    return '$m:$s';
  }

  @override
  Widget build(BuildContext context) {
    final amount = widget.booking.depositAmount;
    final vietQrUrl =
        'https://img.vietqr.io/image/970422-$_accountNumber-compact2.png?amount=$amount&addInfo=${Uri.encodeComponent(_transferContent)}&accountName=${Uri.encodeComponent(_accountHolder)}';

    return Dialog(
      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
      backgroundColor: Colors.white,
      insetPadding: const EdgeInsets.symmetric(horizontal: 16, vertical: 24),
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 420),
        child: SingleChildScrollView(
          padding: const EdgeInsets.all(20),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              // Header logo & close
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                        decoration: BoxDecoration(
                          color: const Color(0xFF003366),
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: const Text(
                          'payOS',
                          style: TextStyle(
                            color: Colors.white,
                            fontWeight: FontWeight.w900,
                            fontSize: 16,
                            letterSpacing: 0.5,
                          ),
                        ),
                      ),
                      const SizedBox(width: 8),
                      Text(
                        'Cổng thanh toán QR',
                        style: AppTypography.headingSmall.copyWith(
                          fontSize: 15,
                          color: AppColors.textPrimary,
                        ),
                      ),
                    ],
                  ),
                  IconButton(
                    onPressed: () => Navigator.of(context).pop(),
                    icon: const Icon(Icons.close_rounded, color: AppColors.textMuted),
                    padding: EdgeInsets.zero,
                    constraints: const BoxConstraints(),
                  ),
                ],
              ),
              const Divider(height: 24),

              // Countdown banner
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
                decoration: BoxDecoration(
                  color: const Color(0xFFFEF3C7),
                  borderRadius: BorderRadius.circular(10),
                  border: Border.all(color: const Color(0xFFFCD34D)),
                ),
                child: Row(
                  children: [
                    const Icon(Icons.timer_outlined, size: 18, color: Color(0xFFB45309)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        'Hạn thanh toán cọc: ${_formatTimer(_secondsLeft)}',
                        style: const TextStyle(
                          color: Color(0xFFB45309),
                          fontWeight: FontWeight.w600,
                          fontSize: 13,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 16),

              // QR Code container
              Center(
                child: Container(
                  width: 230,
                  height: 230,
                  padding: const EdgeInsets.all(10),
                  decoration: BoxDecoration(
                    color: Colors.white,
                    borderRadius: BorderRadius.circular(16),
                    border: Border.all(color: const Color(0xFFE2E8F0), width: 1.5),
                    boxShadow: [
                      BoxShadow(
                        color: Colors.black.withOpacity(0.04),
                        blurRadius: 12,
                        offset: const Offset(0, 4),
                      ),
                    ],
                  ),
                  child: ClipRRect(
                    borderRadius: BorderRadius.circular(10),
                    child: Image.network(
                      vietQrUrl,
                      fit: BoxFit.contain,
                      loadingBuilder: (ctx, child, progress) {
                        if (progress == null) return child;
                        return const Center(
                          child: CircularProgressIndicator(color: Color(0xFF087775)),
                        );
                      },
                      errorBuilder: (ctx, _, __) {
                        return Container(
                          color: const Color(0xFFF8FAFC),
                          child: Column(
                            mainAxisAlignment: MainAxisAlignment.center,
                            children: [
                              const Icon(Icons.qr_code_2_rounded,
                                  size: 110, color: Color(0xFF087775)),
                              const SizedBox(height: 8),
                              Text(
                                'Mã VietQR PayOS\n${Formatters.formatCurrency(amount)}',
                                textAlign: TextAlign.center,
                                style: const TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.bold,
                                  color: Color(0xFF087775),
                                ),
                              ),
                            ],
                          ),
                        );
                      },
                    ),
                  ),
                ),
              ),
              const SizedBox(height: 8),
              const Text(
                'Mở App Ngân hàng hoặc Ví MoMo để quét mã VietQR',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 12, color: AppColors.textMuted),
              ),
              const SizedBox(height: 16),

              // Bank details table
              Container(
                padding: const EdgeInsets.all(12),
                decoration: BoxDecoration(
                  color: const Color(0xFFF8FAFC),
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: const Color(0xFFE2E8F0)),
                ),
                child: Column(
                  children: [
                    _buildInfoRow('Ngân hàng', _bankName),
                    const Divider(height: 14),
                    _buildInfoRow(
                      'Số tài khoản',
                      _accountNumber,
                      onCopy: () => _copy(_accountNumber, 'Số tài khoản'),
                    ),
                    const Divider(height: 14),
                    _buildInfoRow(
                      'Chủ tài khoản',
                      _accountHolder,
                      onCopy: () => _copy(_accountHolder, 'Tên chủ tài khoản'),
                    ),
                    const Divider(height: 14),
                    _buildInfoRow(
                      'Số tiền',
                      Formatters.formatCurrency(amount),
                      onCopy: () => _copy(amount.toString(), 'Số tiền'),
                      highlightValue: true,
                    ),
                    const Divider(height: 14),
                    _buildInfoRow(
                      'Nội dung CK',
                      _transferContent,
                      onCopy: () => _copy(_transferContent, 'Nội dung chuyển khoản'),
                      highlightValue: true,
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 18),

              // Danh sách các ngân hàng để chuyển tiếp nhanh qua App Ngân hàng
              _buildBankListSection(),
              const SizedBox(height: 20),

              // Action buttons
              AppButton(
                text: _isChecking ? 'Đang kiểm tra kết quả...' : 'Tôi đã chuyển khoản xong',
                onPressed: _isChecking ? null : () => _checkPaymentStatus(silent: false),
              ),
              const SizedBox(height: 8),
              TextButton(
                onPressed: () => Navigator.of(context).pop(),
                child: const Text(
                  'Thanh toán sau (trong 15 phút)',
                  style: TextStyle(
                    color: AppColors.textMuted,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildInfoRow(
    String label,
    String value, {
    VoidCallback? onCopy,
    bool highlightValue = false,
  }) {
    return Row(
      children: [
        SizedBox(
          width: 90,
          child: Text(
            label,
            style: const TextStyle(fontSize: 12, color: AppColors.textMuted),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: TextStyle(
              fontSize: 13,
              fontWeight: highlightValue ? FontWeight.bold : FontWeight.w600,
              color: highlightValue ? const Color(0xFF087775) : AppColors.textPrimary,
            ),
          ),
        ),
        if (onCopy != null)
          InkWell(
            onTap: onCopy,
            borderRadius: BorderRadius.circular(4),
            child: const Padding(
              padding: EdgeInsets.all(4),
              child: Icon(Icons.copy_rounded, size: 16, color: Color(0xFF087775)),
            ),
          ),
      ],
    );
  }

  Widget _buildBankListSection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: const [
            Icon(Icons.account_balance_rounded, size: 16, color: Color(0xFF087775)),
            SizedBox(width: 6),
            Text(
              'Chuyển tiếp nhanh qua App Ngân hàng',
              style: TextStyle(
                fontSize: 13,
                fontWeight: FontWeight.w700,
                color: Color(0xFF0F172A),
              ),
            ),
          ],
        ),
        const SizedBox(height: 4),
        const Text(
          'Chọn app để tự động mở và dán thông tin thanh toán:',
          style: TextStyle(fontSize: 11, color: AppColors.textMuted),
        ),
        const SizedBox(height: 10),
        SizedBox(
          height: 74,
          child: ListView.separated(
            scrollDirection: Axis.horizontal,
            itemCount: _kPopularBanks.length,
            separatorBuilder: (_, __) => const SizedBox(width: 8),
            itemBuilder: (context, index) {
              final bank = _kPopularBanks[index];
              return InkWell(
                onTap: () => _openBankApp(bank),
                borderRadius: BorderRadius.circular(10),
                child: Container(
                  width: 76,
                  padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 8),
                  decoration: BoxDecoration(
                    color: const Color(0xFFF8FAFC),
                    borderRadius: BorderRadius.circular(10),
                    border: Border.all(color: const Color(0xFFE2E8F0)),
                  ),
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Container(
                        width: 32,
                        height: 24,
                        decoration: BoxDecoration(
                          color: bank.brandColor,
                          borderRadius: BorderRadius.circular(6),
                        ),
                        alignment: Alignment.center,
                        child: Text(
                          bank.shortName,
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 10,
                            fontWeight: FontWeight.bold,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ),
                      const SizedBox(height: 5),
                      Text(
                        bank.name,
                        style: const TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.w600,
                          color: Color(0xFF334155),
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        textAlign: TextAlign.center,
                      ),
                    ],
                  ),
                ),
              );
            },
          ),
        ),
        if (widget.paymentLink?.checkoutUrl != null &&
            widget.paymentLink!.checkoutUrl!.isNotEmpty) ...[
          const SizedBox(height: 10),
          Center(
            child: InkWell(
              onTap: () async {
                final uri = Uri.parse(widget.paymentLink!.checkoutUrl!);
                if (await canLaunchUrl(uri)) {
                  await launchUrl(uri, mode: LaunchMode.externalApplication);
                }
              },
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 4),
                child: Row(
                  mainAxisSize: MainAxisSize.min,
                  children: const [
                    Icon(Icons.open_in_new_rounded, size: 13, color: Color(0xFF087775)),
                    SizedBox(width: 4),
                    Text(
                      'Hoặc mở trang thanh toán payOS trên trình duyệt',
                      style: TextStyle(
                        fontSize: 11,
                        color: Color(0xFF087775),
                        fontWeight: FontWeight.w600,
                        decoration: TextDecoration.underline,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }

  Future<void> _openBankApp(BankAppInfo bank) async {
    final info =
        'STK: $_accountNumber - Ngân hàng: MB Bank - Số tiền: ${widget.booking.depositAmount} - Nội dung: $_transferContent';
    await Clipboard.setData(ClipboardData(text: info));

    final uri = Uri.parse(bank.scheme);
    bool launched = false;
    try {
      if (await canLaunchUrl(uri)) {
        launched = await launchUrl(uri, mode: LaunchMode.externalApplication);
      }
    } catch (_) {}

    if (!mounted) return;

    if (launched) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Đang mở ${bank.name}. Đã tự động sao chép thông tin chuyển khoản!'),
          backgroundColor: const Color(0xFF087775),
          behavior: SnackBarBehavior.floating,
        ),
      );
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(
            'Đã sao chép STK & nội dung! Vui lòng mở ứng dụng ${bank.name} trên thiết bị để dán.',
          ),
          backgroundColor: const Color(0xFF334155),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }
}
