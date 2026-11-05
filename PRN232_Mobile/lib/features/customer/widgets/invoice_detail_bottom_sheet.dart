import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../models/customer_models.dart';
import 'invoice_complaint_bottom_sheet.dart';
import 'pay_invoice_bottom_sheet.dart';

class InvoiceDetailBottomSheet extends StatelessWidget {
  final InvoiceModel invoice;

  const InvoiceDetailBottomSheet({
    super.key,
    required this.invoice,
  });

  static Future<void> show(BuildContext context, InvoiceModel invoice) {
    return AppBottomSheet.show(
      context: context,
      title: 'Chi tiết hóa đơn',
      subtitle: '${invoice.invoiceCode} • ${invoice.monthYear}',
      child: InvoiceDetailBottomSheet(invoice: invoice),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Status & Due Date Header Card
        AppCard(
          color: invoice.isUnpaid
              ? AppColors.warningBg.withValues(alpha: 0.5)
              : AppColors.successBg.withValues(alpha: 0.5),
          border: BorderSide(
            color: invoice.isUnpaid
                ? const Color(0xFFFDE68A)
                : const Color(0xFFA7F3D0),
          ),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Trạng thái hóa đơn', style: AppTypography.bodySmall),
                  const SizedBox(height: 4),
                  StatusBadge(label: invoice.isUnpaid ? 'Chưa thanh toán' : 'Đã thanh toán'),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text(
                    invoice.isUnpaid ? 'Hạn thanh toán' : 'Ngày thanh toán',
                    style: AppTypography.bodySmall,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    invoice.isUnpaid
                        ? Formatters.formatDate(invoice.dueDate)
                        : Formatters.formatDate(invoice.paidDate),
                    style: AppTypography.labelLarge,
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),

        // Breakdown lines
        Text('Chi tiết các khoản thu', style: AppTypography.headingSmall),
        const SizedBox(height: 10),

        AppCard(
          padding: EdgeInsets.zero,
          child: Column(
            children: invoice.details.map((item) {
              return Column(
                children: [
                  Padding(
                    padding: const EdgeInsets.all(14),
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Expanded(
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text(item.title, style: AppTypography.labelLarge),
                              const SizedBox(height: 2),
                              Text(item.subtitle,
                                  style: AppTypography.bodySmall),
                            ],
                          ),
                        ),
                        Text(
                          Formatters.formatCurrency(item.amount),
                          style: AppTypography.labelLarge
                              .copyWith(color: AppColors.primary),
                        ),
                      ],
                    ),
                  ),
                  if (item != invoice.details.last) const Divider(height: 1),
                ],
              );
            }).toList(),
          ),
        ),
        const SizedBox(height: 16),

        // Total Amount
        AppCard(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
          color: AppColors.primaryLight,
          border: const BorderSide(color: Color(0xFFC7E7E2)),
          child: Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Tổng tiền cần thanh toán:',
                  style: AppTypography.headingSmall),
              Text(
                Formatters.formatCurrency(invoice.totalAmount),
                style: AppTypography.headingLarge
                    .copyWith(color: AppColors.primary),
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),

        // Actions
        if (invoice.isUnpaid) ...[
          AppButton(
            text: 'Thanh toán trực tuyến ngay',
            icon: const Icon(Icons.payment_rounded,
                color: Colors.white, size: 18),
            onPressed: () {
              Navigator.pop(context);
              PayInvoiceBottomSheet.show(context, invoice);
            },
          ),
          const SizedBox(height: 12),
          AppButton(
            text: 'Khiếu nại sai lệch chỉ số',
            variant: AppButtonVariant.outline,
            icon: const Icon(Icons.help_outline_rounded,
                color: AppColors.primary, size: 18),
            onPressed: () {
              Navigator.pop(context);
              InvoiceComplaintBottomSheet.show(context, invoice);
            },
          ),
        ] else ...[
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.surfaceVariant,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                const Icon(Icons.verified_rounded,
                    color: AppColors.success, size: 20),
                const SizedBox(width: 8),
                Text(
                  'Hóa đơn đã được thanh toán qua ${invoice.paymentMethod ?? "VNPay"}',
                  style: AppTypography.labelMedium
                      .copyWith(color: AppColors.successText),
                ),
              ],
            ),
          ),
        ],
        const SizedBox(height: 10),
      ],
    );
  }
}
