import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_constants.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/invoice_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../routes/app_routes.dart';
import '../models/customer_models.dart';

class PayInvoiceBottomSheet extends StatefulWidget {
  final InvoiceModel invoice;

  const PayInvoiceBottomSheet({super.key, required this.invoice});

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
  final InvoiceService _invoiceService = InvoiceService();
  String _selectedMethod = AppConstants.paymentVNPay;
  bool _isProcessing = false;

  void _pay() async {
    setState(() => _isProcessing = true);

    try {
      final paidInvoice = await _invoiceService.payInvoice(
        widget.invoice.invoiceId,
        _selectedMethod,
      );

      if (!mounted) return;
      setState(() => _isProcessing = false);

      Navigator.pop(context); // close bottom sheet

      context.push(
        AppRoutes.paymentSuccess,
        extra: {
          'title': 'Thanh toán thành công!',
          'subtitle': 'Đã hoàn tất thanh toán hóa đơn ${widget.invoice.monthYear}',
          'amount': paidInvoice.totalAmount,
          'method': _selectedMethod,
          'code': paidInvoice.invoiceCode,
        },
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _isProcessing = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
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
        const SizedBox(height: 18),

        // Method selector
        Text('Chọn phương thức thanh toán', style: AppTypography.labelLarge),
        const SizedBox(height: 8),

        _buildMethodOption(
          method: AppConstants.paymentVNPay,
          title: 'VNPay QR / Thẻ nội địa ATM',
          subtitle: 'Quét mã qua ứng dụng ngân hàng hoặc VNPAY',
          icon: Icons.qr_code_2_rounded,
          iconColor: const Color(0xFF005BAA),
        ),
        const SizedBox(height: 8),

        _buildMethodOption(
          method: AppConstants.paymentMoMo,
          title: 'Ví Điện Tử MoMo',
          subtitle: 'Thanh toán siêu tốc 1 chạm qua ứng dụng MoMo',
          icon: Icons.account_balance_wallet_rounded,
          iconColor: const Color(0xFFA50064),
        ),
        const SizedBox(height: 24),

        // Submit
        AppButton(
          text: 'Xác nhận thanh toán ngay',
          isLoading: _isProcessing,
          onPressed: _pay,
        ),
        const SizedBox(height: 12),
      ],
    );
  }

  Widget _buildMethodOption({
    required String method,
    required String title,
    required String subtitle,
    required IconData icon,
    required Color iconColor,
  }) {
    final isSelected = _selectedMethod == method;
    return InkWell(
      onTap: () => setState(() => _selectedMethod = method),
      borderRadius: BorderRadius.circular(12),
      child: Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          borderRadius: BorderRadius.circular(12),
          border: Border.all(
            color: isSelected ? AppColors.primary : AppColors.border,
            width: isSelected ? 2 : 1,
          ),
          color: isSelected ? AppColors.surfaceVariant : Colors.white,
        ),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: iconColor.withValues(alpha: 0.1),
                borderRadius: BorderRadius.circular(8),
              ),
              child: Icon(icon, color: iconColor, size: 24),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(title, style: AppTypography.labelLarge),
                  Text(subtitle, style: AppTypography.bodySmall),
                ],
              ),
            ),
            Radio<String>(
              value: method,
              groupValue: _selectedMethod,
              activeColor: AppColors.primary,
              onChanged: (val) {
                if (val != null) setState(() => _selectedMethod = val);
              },
            ),
          ],
        ),
      ),
    );
  }
}
