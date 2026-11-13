import 'package:flutter/material.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/contract_service.dart';
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
  final ContractService _contractService = ContractService();
  List<CustomerContractModel> _contracts = [];
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _contracts = AuthState.isCurrentDemo ? List.from(CustomerMockData.myContracts) : [];
    _loadContracts();
  }

  Future<void> _loadContracts() async {
    setState(() => _isLoading = true);
    try {
      final list = await _contractService.getCustomerContracts();
      if (mounted) {
        setState(() {
          _contracts = list;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
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
                        Text('Khách thuê', style: AppTypography.bodySmall),
                        Text(contract.primaryTenantName, style: AppTypography.headingSmall),
                        Text(contract.tenantPhone, style: AppTypography.bodySmall),
                      ],
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text('Tiền phòng/tháng', style: AppTypography.bodySmall),
                        Text(Formatters.formatCurrency(contract.monthlyRent),
                            style: AppTypography.headingSmall.copyWith(color: AppColors.primary)),
                        Text('Cọc: ${Formatters.formatCurrency(contract.depositAmount)}',
                            style: AppTypography.bodySmall),
                      ],
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),

          // Timeline công nợ các kỳ
          Text('Tiến độ thanh toán tiền thuê (Billing Timeline)', style: AppTypography.headingSmall),
          const SizedBox(height: 8),
          if (contract.billingTimeline.isEmpty)
            Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: AppColors.surfaceVariant,
                borderRadius: BorderRadius.circular(10),
              ),
              child: const Text('Kỳ thanh toán tự động tạo vào ngày 01 hàng tháng.'),
            )
          else
            ...contract.billingTimeline.map((item) {
              return Container(
                margin: const EdgeInsets.only(bottom: 8),
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
                decoration: BoxDecoration(
                  color: item.isPaid ? AppColors.surfaceVariant : Colors.white,
                  border: Border.all(color: item.isPaid ? Colors.transparent : AppColors.primary),
                  borderRadius: BorderRadius.circular(10),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Row(
                      children: [
                        Icon(
                          item.isPaid ? Icons.check_circle_rounded : Icons.pending_actions_rounded,
                          size: 20,
                          color: item.isPaid ? AppColors.success : AppColors.warning,
                        ),
                        const SizedBox(width: 10),
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(item.periodName, style: AppTypography.labelLarge),
                            Text('Hạn: ${Formatters.formatDate(item.dueDate)}',
                                style: AppTypography.bodySmall),
                          ],
                        ),
                      ],
                    ),
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.end,
                      children: [
                        Text(Formatters.formatCurrency(item.amount), style: AppTypography.labelLarge),
                        Text(
                          item.isPaid ? 'Đã thanh toán' : 'Chưa thu',
                          style: AppTypography.labelSmall.copyWith(
                            color: item.isPaid ? AppColors.success : AppColors.danger,
                          ),
                        ),
                      ],
                    ),
                  ],
                ),
              );
            }),
          const SizedBox(height: 16),

          // Terms
          Text('Điều khoản thuê', style: AppTypography.headingSmall),
          const SizedBox(height: 6),
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: AppColors.surfaceVariant,
              borderRadius: BorderRadius.circular(10),
            ),
            child: Text(contract.terms, style: AppTypography.bodySmall),
          ),
          const SizedBox(height: 24),
          AppButton(
            text: 'Đóng',
            variant: AppButtonVariant.secondary,
            onPressed: () => Navigator.pop(context),
          ),
          const SizedBox(height: 12),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Hợp Đồng Của Tôi'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: AppColors.primary))
          : RefreshIndicator(
              onRefresh: _loadContracts,
              color: AppColors.primary,
              child: _contracts.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.assignment_late_outlined, size: 64, color: AppColors.textMuted),
                          const SizedBox(height: 12),
                          Text('Bạn chưa có hợp đồng thuê nào', style: AppTypography.bodyMedium),
                        ],
                      ),
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(16),
                      itemCount: _contracts.length,
                      separatorBuilder: (context, index) => const SizedBox(height: 12),
                      itemBuilder: (context, index) {
                        final c = _contracts[index];
                        return _buildContractCard(c);
                      },
                    ),
            ),
    );
  }

  Widget _buildContractCard(CustomerContractModel contract) {
    return AppCard(
      onTap: () => _showContractDetail(contract),
      padding: const EdgeInsets.all(16),
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
