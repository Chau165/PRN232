import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../routes/app_routes.dart';

class PaymentSuccessScreen extends StatelessWidget {
  final String title;
  final String subtitle;
  final num amount;
  final String method;
  final String code;

  const PaymentSuccessScreen({
    super.key,
    required this.title,
    required this.subtitle,
    required this.amount,
    required this.method,
    required this.code,
  });

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SafeArea(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisAlignment: MainAxisAlignment.center,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Spacer(),
              // Animated Success Icon
              Center(
                child: Container(
                  width: 90,
                  height: 90,
                  decoration: const BoxDecoration(
                    color: AppColors.successBg,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.check_circle_rounded,
                    color: AppColors.success,
                    size: 56,
                  ),
                ),
              ),
              const SizedBox(height: 24),

              Text(
                title,
                style: AppTypography.displayLarge.copyWith(fontSize: 26),
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 8),
              Text(
                subtitle,
                style: AppTypography.bodyMedium,
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 32),

              // Transaction Receipt Card
              AppCard(
                padding: const EdgeInsets.all(18),
                child: Column(
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text('Số tiền giao dịch:',
                            style: AppTypography.bodyMedium),
                        Text(
                          Formatters.formatCurrency(amount),
                          style: AppTypography.headingLarge
                              .copyWith(color: AppColors.primary),
                        ),
                      ],
                    ),
                    const Divider(height: 20),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text('Cổng thanh toán:',
                            style: AppTypography.bodySmall),
                        Text(method, style: AppTypography.labelLarge),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text('Mã giao dịch:', style: AppTypography.bodySmall),
                        Text(code, style: AppTypography.labelMedium),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Text('Thời gian:', style: AppTypography.bodySmall),
                        Text(
                          Formatters.formatDateTime(DateTime.now()),
                          style: AppTypography.bodySmall,
                        ),
                      ],
                    ),
                  ],
                ),
              ),

              const Spacer(),

              AppButton(
                text: 'Xem phòng của tôi',
                onPressed: () => context.go(AppRoutes.customerMyRoom),
              ),
              const SizedBox(height: 12),
              AppButton(
                text: 'Về trang chủ tìm phòng',
                variant: AppButtonVariant.outline,
                onPressed: () => context.go(AppRoutes.customerExplore),
              ),
              const SizedBox(height: 12),
            ],
          ),
        ),
      ),
    );
  }
}
