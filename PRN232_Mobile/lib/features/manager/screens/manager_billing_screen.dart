import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';

class ManagerBillingScreen extends StatelessWidget {
  const ManagerBillingScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Quick Meter Reading Action (Tính năng cơ động cho Manager)
            AppCard(
              color: AppColors.primary,
              border: BorderSide.none,
              padding: const EdgeInsets.all(18),
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.18),
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: const Icon(Icons.speed_rounded,
                        color: Colors.white, size: 28),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Ghi số điện nước tháng 11',
                          style: AppTypography.headingSmall
                              .copyWith(color: Colors.white),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          'Còn 14/35 phòng chưa nhập số',
                          style: AppTypography.bodySmall
                              .copyWith(color: Colors.white70),
                        ),
                      ],
                    ),
                  ),
                  ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.white,
                      foregroundColor: AppColors.primary,
                      elevation: 0,
                      padding: const EdgeInsets.symmetric(
                          horizontal: 14, vertical: 8),
                      minimumSize: const Size(80, 38),
                    ),
                    onPressed: () {
                      ScaffoldMessenger.of(context).showSnackBar(
                        const SnackBar(
                          content: Text(
                              'Tính năng Ghi chỉ số cơ động sẽ hoàn thiện trong Giai đoạn 4!'),
                        ),
                      );
                    },
                    child: Text('Bắt đầu ghi',
                        style: AppTypography.labelSmall
                            .copyWith(color: AppColors.primary)),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Billing stats summary
            Row(
              children: [
                Expanded(
                  child: AppCard(
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Đã thu tiền', style: AppTypography.bodySmall),
                        const SizedBox(height: 4),
                        Text('28.4 tr',
                            style: AppTypography.headingLarge
                                .copyWith(color: AppColors.success)),
                        Text('24/35 phòng',
                            style: AppTypography.bodySmall
                                .copyWith(fontSize: 11)),
                      ],
                    ),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: AppCard(
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Chưa thu tiền', style: AppTypography.bodySmall),
                        const SizedBox(height: 4),
                        Text('14.1 tr',
                            style: AppTypography.headingLarge
                                .copyWith(color: AppColors.warning)),
                        Text('11 phòng chờ thu',
                            style: AppTypography.bodySmall
                                .copyWith(fontSize: 11)),
                      ],
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 24),

            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Hóa đơn tháng 11', style: AppTypography.headingMedium),
                Text('Lập hóa đơn mới +',
                    style: AppTypography.labelMedium
                        .copyWith(color: AppColors.primary)),
              ],
            ),
            const SizedBox(height: 12),

            _buildInvoiceItem(
              room: 'Phòng 204',
              tenant: 'Nguyễn Văn An',
              amount: 3462000,
              status: 'Unpaid',
            ),
            const SizedBox(height: 10),
            _buildInvoiceItem(
              room: 'Phòng 101',
              tenant: 'Trần Minh Quân',
              amount: 3820000,
              status: 'Paid',
            ),
            const SizedBox(height: 10),
            _buildInvoiceItem(
              room: 'Phòng 205',
              tenant: 'Hoàng Văn Đức',
              amount: 3100000,
              status: 'Overdue',
            ),
          ],
        ),
      ),
    );
  }

  Widget _buildInvoiceItem({
    required String room,
    required String tenant,
    required num amount,
    required String status,
  }) {
    return AppCard(
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: AppColors.surfaceVariant,
                  borderRadius: BorderRadius.circular(10),
                ),
                child: const Icon(Icons.receipt_outlined,
                    color: AppColors.primary, size: 22),
              ),
              const SizedBox(width: 12),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(room, style: AppTypography.headingSmall),
                  Text(tenant, style: AppTypography.bodySmall),
                ],
              ),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                Formatters.formatCurrency(amount),
                style: AppTypography.labelLarge,
              ),
              const SizedBox(height: 4),
              StatusBadge(label: status),
            ],
          ),
        ],
      ),
    );
  }
}
