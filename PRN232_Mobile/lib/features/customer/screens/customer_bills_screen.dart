import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';

class CustomerBillsScreen extends StatelessWidget {
  const CustomerBillsScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Unpaid Bill Highlight
            AppCard(
              border: const BorderSide(color: Color(0xFFFDE68A), width: 1.5),
              color: AppColors.warningBg.withValues(alpha: 0.5),
              padding: const EdgeInsets.all(18),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Hóa đơn tháng 11/2026',
                          style: AppTypography.headingMedium),
                      const StatusBadge(label: 'Chưa thanh toán'),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Text('Hạn thanh toán: 10/11/2026 (Còn 5 ngày)',
                      style: AppTypography.bodySmall
                          .copyWith(color: AppColors.warningText)),
                  const Divider(height: 24),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text('Tổng số tiền:',
                              style: AppTypography.bodySmall),
                          Text(
                            Formatters.formatCurrency(3462000),
                            style: AppTypography.headingLarge
                                .copyWith(color: AppColors.primary),
                          ),
                        ],
                      ),
                      AppButton(
                        text: 'Thanh toán',
                        height: 40,
                        onPressed: () {
                          ScaffoldMessenger.of(context).showSnackBar(
                            const SnackBar(
                              content: Text('Cổng thanh toán MoMo/VNPay sẽ hoàn thiện trong Giai đoạn 3!'),
                            ),
                          );
                        },
                      ),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            Text('Lịch sử thanh toán', style: AppTypography.headingMedium),
            const SizedBox(height: 12),

            _buildPaidBillCard(
              month: 'Tháng 10/2026',
              paidDate: '05/10/2026',
              amount: 3410000,
              method: 'VNPay',
            ),
            const SizedBox(height: 12),

            _buildPaidBillCard(
              month: 'Tháng 09/2026',
              paidDate: '06/09/2026',
              amount: 3385000,
              method: 'MoMo',
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildPaidBillCard({
    required String month,
    required String paidDate,
    required num amount,
    required String method,
  }) {
    return AppCard(
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: const BoxDecoration(
                  color: AppColors.successBg,
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.check_circle_outline,
                    color: AppColors.success, size: 22),
              ),
              const SizedBox(width: 12),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(month, style: AppTypography.labelLarge),
                  Text('Đã trả ngày $paidDate • $method',
                      style: AppTypography.bodySmall),
                ],
              ),
            ],
          ),
          Text(
            Formatters.formatCurrency(amount),
            style: AppTypography.labelLarge,
          ),
        ],
      ),
    );
  }
}
