import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/services/invoice_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../routes/app_routes.dart';
import '../models/customer_models.dart';

class PayInvoiceBottomSheet extends StatefulWidget {
  final InvoiceModel invoice;

  const PayInvoiceBottomSheet({super.key, required this.invoice});

  static Future<void> show(BuildContext context, InvoiceModel invoice) {
    return showModalBottomSheet(
      context: context,
      isScrollControlled: true,
      backgroundColor: Colors.transparent,
      builder: (context) => PayInvoiceBottomSheet(invoice: invoice),
    );
  }

  @override
  State<PayInvoiceBottomSheet> createState() => _PayInvoiceBottomSheetState();
}

class _PayInvoiceBottomSheetState extends State<PayInvoiceBottomSheet> {
  final InvoiceService _invoiceService = InvoiceService();
  bool _isProcessing = false;
  String? _error;

  Future<void> _pay() async {
    if (_isProcessing) return;

    setState(() {
      _isProcessing = true;
      _error = null;
    });

    try {
      final link = await _invoiceService.createPaymentLink(widget.invoice.invoiceId);

      if (!mounted) return;

      if (link.checkoutUrl == null || link.checkoutUrl!.isEmpty) {
        throw Exception('Không nhận được liên kết thanh toán từ payOS');
      }

      // 1. Mở trang checkout payOS thật với mã VietQR PRO
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

      // 2. Đóng bottom sheet
      Navigator.of(context).pop();

      // 3. Chuyển sang màn hình "Đang chờ xác nhận thanh toán" giống Web
      context.push(
        AppRoutes.paymentResult,
        extra: {
          'requestId': link.paymentRequestId,
          'invoiceId': widget.invoice.invoiceId,
          'amount': link.amount > 0 ? link.amount : widget.invoice.payableAmount,
          'checkoutUrl': link.checkoutUrl,
          'isBooking': false,
        },
      );
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isProcessing = false;
        _error = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final payableAmount = widget.invoice.payableAmount;
    final roomCode = widget.invoice.roomCode.isNotEmpty ? widget.invoice.roomCode : 'N/A';

    return Container(
      decoration: const BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      padding: EdgeInsets.only(
        left: 24,
        right: 24,
        top: 16,
        bottom: MediaQuery.of(context).viewInsets.bottom + 24,
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Drag handle
          Center(
            child: Container(
              width: 40,
              height: 4,
              decoration: BoxDecoration(
                color: const Color(0xFFE2E8F0),
                borderRadius: BorderRadius.circular(2),
              ),
            ),
          ),
          const SizedBox(height: 20),

          // Header Tag: HĐ #7 · PHÒNG BD-A101
          Text(
            'HĐ #${widget.invoice.invoiceId} · PHÒNG $roomCode',
            style: const TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w700,
              letterSpacing: 1.2,
              color: Color(0xFF087775),
            ),
          ),
          const SizedBox(height: 8),

          // Title: Thanh toán hóa đơn tháng ...
          Text(
            'Thanh toán hóa đơn ${widget.invoice.monthYear}',
            style: const TextStyle(
              fontSize: 20,
              fontWeight: FontWeight.w800,
              color: Color(0xFF0F172A),
            ),
          ),
          const SizedBox(height: 8),

          // Subtitle: Số tiền cần thanh toán
          Text.rich(
            TextSpan(
              text: 'Số tiền cần thanh toán: ',
              style: const TextStyle(fontSize: 14, color: Color(0xFF64748B)),
              children: [
                TextSpan(
                  text: Formatters.formatCurrency(payableAmount),
                  style: const TextStyle(
                    fontWeight: FontWeight.w800,
                    color: Color(0xFF087775),
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 18),

          // Notice banner
          Container(
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: const Color(0xFFF0FDFA),
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: const Color(0xFFCCFBF1)),
            ),
            child: const Text(
              'Bạn sẽ được chuyển đến payOS để thanh toán. Hóa đơn chỉ được ghi nhận sau khi hệ thống xác nhận đã nhận tiền.',
              style: TextStyle(
                fontSize: 13,
                color: Color(0xFF334155),
                height: 1.5,
              ),
            ),
          ),

          if (_error != null) ...[
            const SizedBox(height: 12),
            Text(
              _error!,
              style: const TextStyle(
                fontSize: 13,
                color: Color(0xFFE11D48),
                fontWeight: FontWeight.w500,
              ),
            ),
          ],

          const SizedBox(height: 24),

          // Button Row: [ Đóng ]  [ Thanh toán qua payOS ]
          Row(
            children: [
              TextButton(
                onPressed: _isProcessing ? null : () => Navigator.of(context).pop(),
                style: TextButton.styleFrom(
                  backgroundColor: const Color(0xFFF1F5F9),
                  foregroundColor: const Color(0xFF475569),
                  padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
                child: const Text(
                  'Đóng',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: ElevatedButton(
                  onPressed: _isProcessing ? null : _pay,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF087775),
                    foregroundColor: Colors.white,
                    disabledBackgroundColor: const Color(0xFF087775).withOpacity(0.6),
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                    elevation: 0,
                  ),
                  child: _isProcessing
                      ? const Row(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            SizedBox(
                              width: 18,
                              height: 18,
                              child: CircularProgressIndicator(
                                color: Colors.white,
                                strokeWidth: 2,
                              ),
                            ),
                            SizedBox(width: 10),
                            Text(
                              'Đang tạo liên kết...',
                              style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
                            ),
                          ],
                        )
                      : const Text(
                          'Thanh toán qua payOS',
                          style: TextStyle(fontWeight: FontWeight.w700, fontSize: 14),
                        ),
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
