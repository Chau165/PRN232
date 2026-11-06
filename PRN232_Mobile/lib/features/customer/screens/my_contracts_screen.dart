import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';

class MyContractsScreen extends StatefulWidget {
  const MyContractsScreen({super.key});

  @override
  State<MyContractsScreen> createState() => _MyContractsScreenState();
}

class _MyContractsScreenState extends State<MyContractsScreen> {
  late List<CustomerContractModel> _contracts;

  @override
  void initState() {
    super.initState();
    _contracts = List.from(CustomerMockData.myContracts);
  }

  void _showContractDetail(CustomerContractModel contract) {
    AppBottomSheet.show(
      context: context,
      title: 'Chi tiết hợp đồng',
      subtitle: '${contract.contractCode} • ${contract.roomCode}',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          // Header summary
          AppCard(
            color: AppColors.primaryLight,
            border: const BorderSide(color: Color(0xFFC7E7E2)),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(contract.contractCode,
                        style: AppTypography.headingLarge.copyWith(color: AppColors.primary)),
                    StatusBadge(
                      label: contract.isActive ? 'Đang hiệu lực' : 'Đã thanh lý',
                      tone: contract.isActive ? StatusTone.green : StatusTone.slate,
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Text('${contract.roomCode} • ${contract.propertyName}',
                    style: AppTypography.labelLarge),
                Text(contract.propertyAddress, style: AppTypography.bodySmall),
                const Divider(height: 20),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Tiền thuê/tháng', style: AppTypography.bodySmall),
                        Text(Formatters.formatCurrency(contract.monthlyRent),
                            style: AppTypography.headingSmall),
                      ],
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text('Tiền cọc giữ chân', style: AppTypography.bodySmall),
                        Text(Formatters.formatCurrency(contract.depositAmount),
                            style: AppTypography.labelLarge.copyWith(color: AppColors.primary)),
                      ],
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 18),

          // Parties information
          Text('Thông tin các bên', style: AppTypography.headingSmall),
          const SizedBox(height: 8),
          AppCard(
            padding: const EdgeInsets.all(12),
            child: Column(
              children: [
                _buildInfoRow('Bên cho thuê (Bên A):', contract.propertyName),
                const Divider(height: 14),
                _buildInfoRow('Đại diện thuê (Bên B):', contract.primaryTenantName),
                const Divider(height: 14),
                _buildInfoRow('Số CCCD / Định danh:', contract.tenantCccd),
                const Divider(height: 14),
                _buildInfoRow('Số điện thoại liên lạc:', contract.tenantPhone),
                const Divider(height: 14),
                _buildInfoRow(
                  'Thời hạn hợp đồng:',
                  '${Formatters.formatDate(contract.startDate)} → ${Formatters.formatDate(contract.endDate)}',
                ),
              ],
            ),
          ),
          const SizedBox(height: 18),

          // Contract terms
          Text('Điều khoản hợp đồng', style: AppTypography.headingSmall),
          const SizedBox(height: 8),
          AppCard(
            padding: const EdgeInsets.all(14),
            child: Text(
              contract.terms,
              style: AppTypography.bodySmall.copyWith(height: 1.5),
            ),
          ),
          const SizedBox(height: 18),

          // Billing Timeline
          if (contract.billingTimeline.isNotEmpty) ...[
            Text('Dòng thời gian công nợ kỳ thuê', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              padding: EdgeInsets.zero,
              child: ListView.separated(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: contract.billingTimeline.length,
                separatorBuilder: (_, _) => const Divider(height: 1),
                itemBuilder: (ctx, idx) {
                  final item = contract.billingTimeline[idx];
                  return ListTile(
                    leading: Icon(
                      item.isPaid ? Icons.check_circle_rounded : Icons.pending_actions_rounded,
                      color: item.isPaid ? AppColors.success : AppColors.warning,
                      size: 22,
                    ),
                    title: Text(item.periodName, style: AppTypography.labelMedium),
                    subtitle: Text('Hạn đóng: ${Formatters.formatDate(item.dueDate)}',
                        style: AppTypography.bodySmall),
                    trailing: Text(
                      Formatters.formatCurrency(item.amount),
                      style: AppTypography.labelMedium.copyWith(
                        color: item.isPaid ? AppColors.textPrimary : AppColors.warning,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  );
                },
              ),
            ),
            const SizedBox(height: 18),
          ],

          AppButton(
            text: 'Đóng chi tiết',
            variant: AppButtonVariant.outline,
            onPressed: () => Navigator.pop(context),
          ),
          const SizedBox(height: 10),
        ],
      ),
    );
  }

  Widget _buildInfoRow(String label, String value) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(label, style: AppTypography.bodySmall.copyWith(color: AppColors.textSecondary)),
        Text(value, style: AppTypography.labelMedium),
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final activeContracts = _contracts.where((c) => c.isActive).toList();
    final pastContracts = _contracts.where((c) => !c.isActive).toList();

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Hợp Đồng Thuê Của Tôi'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          // Active Contracts
          Text('Hợp đồng đang hiệu lực', style: AppTypography.headingMedium),
          const SizedBox(height: 10),
          if (activeContracts.isEmpty)
            const AppCard(
              child: Center(
                child: Text('Hiện tại bạn chưa có hợp đồng thuê nào đang hoạt động'),
              ),
            )
          else
            ...activeContracts.map((c) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _buildContractCard(c),
                )),
          const SizedBox(height: 24),

          // Past Contracts History
          if (pastContracts.isNotEmpty) ...[
            Text('Lịch sử hợp đồng trước đây', style: AppTypography.headingMedium),
            const SizedBox(height: 10),
            ...pastContracts.map((c) => Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _buildContractCard(c),
                )),
          ],
        ],
      ),
    );
  }

  Widget _buildContractCard(CustomerContractModel contract) {
    return AppCard(
      onTap: () => _showContractDetail(contract),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      contract.contractCode,
                      style: AppTypography.labelSmall.copyWith(color: AppColors.primary),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text(contract.roomCode, style: AppTypography.headingSmall),
                ],
              ),
              StatusBadge(
                label: contract.isActive ? 'Đang hiệu lực' : 'Đã thanh lý',
                tone: contract.isActive ? StatusTone.green : StatusTone.slate,
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text('${contract.roomTypeName} • ${contract.propertyName}',
              style: AppTypography.bodySmall),
          const Divider(height: 20),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Thời hạn', style: AppTypography.bodySmall),
                  Text(
                    '${Formatters.formatDate(contract.startDate)} - ${Formatters.formatDate(contract.endDate)}',
                    style: AppTypography.labelLarge,
                  ),
                ],
              ),
              Column(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Text('Tiền phòng/tháng', style: AppTypography.bodySmall),
                  Text(
                    Formatters.formatCurrency(contract.monthlyRent),
                    style: AppTypography.headingSmall.copyWith(color: AppColors.primary),
                  ),
                ],
              ),
            ],
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                'Cọc: ${Formatters.formatCurrency(contract.depositAmount)}',
                style: AppTypography.bodySmall.copyWith(color: AppColors.textMuted),
              ),
              Text(
                'Xem điều khoản & công nợ →',
                style: AppTypography.labelSmall.copyWith(color: AppColors.primary),
              ),
            ],
          ),
        ],
      ),
    );
  }
}
