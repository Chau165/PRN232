import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_constants.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../routes/app_routes.dart';
import '../models/customer_models.dart';

class PayInvoiceBottomSheet extends StatefulWidget {
  final InvoiceModel invoice;

  const PayInvoiceBottomSheet({
    super.key,
    required this.invoice,
  });

  static Future<void> show(BuildContext context, InvoiceModel invoice) {
    return AppBottomSheet.show(
      context: context,
      title: 'Thanh toán hóa đơn',
      subtitle: '${invoice.invoiceCode} • ${invoice.monthYear}',
      child: PayInvoiceBottomSheet(invoice: invoice),
    );
  }

  @override
  State<PayInvoiceBottomSheet> createState() => _PayInvoiceBottomSheetState();
}

class _PayInvoiceBottomSheetState extends State<PayInvoiceBottomSheet> {
  String _selectedMethod = AppConstants.paymentVNPay;
  bool _isProcessing = false;

  void _pay() async {
    setState(() => _isProcessing = true);
    await Future.delayed(const Duration(milliseconds: 900));
    if (!mounted) return;
    setState(() => _isProcessing = false);

    Navigator.pop(context); // close bottom sheet

    context.push(
      AppRoutes.paymentSuccess,
      extra: {
        'title': 'Thanh toán thành công!',
        'subtitle': 'Đã hoàn tất thanh toán hóa đơn ${widget.invoice.monthYear}',
        'amount': widget.invoice.totalAmount,
        'method': _selectedMethod,
        'code': 'INV-${DateTime.now().millisecondsSinceEpoch.toString().substring(7)}',
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Total highlight
        AppCard(
          color: AppColors.primaryLight,
          border: const BorderSide(color: Color(0xFFC7E7E2)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('Số tiền thanh toán', style: AppTypography.bodySmall),
              const SizedBox(height: 4),
              Text(
                Formatters.formatCurrency(widget.invoice.totalAmount),
                style: AppTypography.displayLarge
                    .copyWith(color: AppColors.primary, fontSize: 26),
              ),
              const SizedBox(height: 4),
              Text(
                'Bao gồm tiền phòng và điện nước tháng ${widget.invoice.monthYear}',
                style: AppTypography.bodySmall,
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),

        Text('Chọn phương thức thanh toán', style: AppTypography.headingSmall),
        const SizedBox(height: 10),

        AppCard(
          padding: EdgeInsets.zero,
          child: Column(
            children: [
              _buildPaymentOption(
                method: AppConstants.paymentVNPay,
                label: 'Cổng VNPAY (Thẻ ATM / QR)',
                badgeText: 'VNPAY',
                badgeBg: const Color(0xFFE0F2FE),
                badgeFg: const Color(0xFF0369A1),
              ),
              const Divider(height: 1),
              _buildPaymentOption(
                method: AppConstants.paymentMoMo,
                label: 'Ví Điện Tử MoMo',
                badgeText: 'MOMO',
                badgeBg: const Color(0xFFFCE7F3),
                badgeFg: const Color(0xFFBE185D),
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),

        AppButton(
          text:
              'Xác nhận thanh toán • ${Formatters.formatCurrency(widget.invoice.totalAmount)}',
          isLoading: _isProcessing,
          onPressed: _pay,
        ),
        const SizedBox(height: 10),
      ],
    );
  }

  Widget _buildPaymentOption({
    required String method,
    required String label,
    required String badgeText,
    required Color badgeBg,
    required Color badgeFg,
  }) {
    final isSelected = _selectedMethod == method;
    return InkWell(
      onTap: () {
        setState(() => _selectedMethod = method);
      },
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: badgeBg,
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                badgeText,
                style: TextStyle(
                  fontWeight: FontWeight.w900,
                  color: badgeFg,
                  fontSize: 12,
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(label, style: AppTypography.labelLarge),
            ),
            Container(
              width: 20,
              height: 20,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(
                  color: isSelected ? AppColors.primary : AppColors.border,
                  width: 2,
                ),
                color: isSelected ? AppColors.primary : Colors.transparent,
              ),
              child: isSelected
                  ? const Icon(Icons.check, size: 14, color: Colors.white)
                  : null,
            ),
          ],
        ),
      ),
    );
  }
}
