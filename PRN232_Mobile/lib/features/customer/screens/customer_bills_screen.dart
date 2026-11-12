import 'package:flutter/material.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/invoice_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';
import '../widgets/invoice_detail_bottom_sheet.dart';
import '../widgets/pay_invoice_bottom_sheet.dart';

class CustomerBillsScreen extends StatefulWidget {
  const CustomerBillsScreen({super.key});

  @override
  State<CustomerBillsScreen> createState() => _CustomerBillsScreenState();
}

class _CustomerBillsScreenState extends State<CustomerBillsScreen> {
  final InvoiceService _invoiceService = InvoiceService();
  List<InvoiceModel> _invoices = [];
  bool _isLoading = false;
  int _selectedFilterIndex = 0; // 0: Tất cả, 1: Chưa thanh toán, 2: Đã thanh toán

  final List<String> _filters = ['Tất cả', 'Chưa thanh toán', 'Đã thanh toán'];

  @override
  void initState() {
    super.initState();
    _invoices = AuthState.isCurrentDemo ? List.from(CustomerMockData.myInvoices) : [];
    _loadInvoices();
  }

  Future<void> _loadInvoices() async {
    setState(() => _isLoading = true);
    try {
      final list = await _invoiceService.getCustomerInvoices();
      if (mounted) {
        setState(() {
          _invoices = list;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  List<InvoiceModel> get _filteredInvoices {
    if (_selectedFilterIndex == 1) {
      return _invoices.where((inv) => inv.isUnpaid).toList();
    } else if (_selectedFilterIndex == 2) {
      return _invoices.where((inv) => !inv.isUnpaid).toList();
    }
    return _invoices;
  }

  InvoiceModel? get _pendingInvoice {
    try {
      return _invoices.firstWhere((inv) => inv.isUnpaid);
    } catch (_) {
      return null;
    }
  }

  void _openInvoiceDetail(InvoiceModel invoice) {
    InvoiceDetailBottomSheet.show(context, invoice);
  }

  void _openQuickPay(InvoiceModel invoice) {
    PayInvoiceBottomSheet.show(context, invoice);
  }

  @override
  Widget build(BuildContext context) {
    final pendingInv = _pendingInvoice;

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Hóa Đơn & Thanh Toán'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: AppColors.primary))
          : _invoices.isEmpty
              ? RefreshIndicator(
                  onRefresh: _loadInvoices,
                  color: AppColors.primary,
                  child: Center(
                    child: SingleChildScrollView(
                      physics: const AlwaysScrollableScrollPhysics(),
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Container(
                            width: 80,
                            height: 80,
                            decoration: const BoxDecoration(
                              color: AppColors.primaryLight,
                              shape: BoxShape.circle,
                            ),
                            child: const Icon(Icons.receipt_long_outlined, size: 40, color: AppColors.primary),
                          ),
                          const SizedBox(height: 16),
                          Text('Bạn chưa có hóa đơn nào',
                              style: AppTypography.headingMedium, textAlign: TextAlign.center),
                          const SizedBox(height: 8),
                          Text(
                            'Khi bạn thuê phòng và phát sinh kỳ thanh toán tiền phòng hoặc tiền điện nước, các hóa đơn sẽ xuất hiện tại đây.',
                            style: AppTypography.bodyMedium.copyWith(color: AppColors.textSecondary),
                            textAlign: TextAlign.center,
                          ),
                        ],
                      ),
                    ),
                  ),
                )
              : RefreshIndicator(
              onRefresh: _loadInvoices,
              color: AppColors.primary,
              child: SingleChildScrollView(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Highlight Pending Bill
            if (pendingInv != null) ...[
              AppCard(
                border: const BorderSide(color: Color(0xFFFDE68A), width: 1.5),
                color: AppColors.warningBg.withValues(alpha: 0.5),
                padding: const EdgeInsets.all(18),
                onTap: () => _openInvoiceDetail(pendingInv),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Row(
                          children: [
                            Container(
                              padding: const EdgeInsets.all(8),
                              decoration: BoxDecoration(
                                color: AppColors.warning.withValues(alpha: 0.2),
                                borderRadius: BorderRadius.circular(8),
                              ),
                              child: const Icon(Icons.receipt_long_rounded,
                                  color: AppColors.warning, size: 22),
                            ),
                            const SizedBox(width: 10),
                            Text('Hóa đơn ${pendingInv.monthYear}',
                                style: AppTypography.headingMedium),
                          ],
                        ),
                        const StatusBadge(label: 'Chưa thanh toán'),
                      ],
                    ),
                    const SizedBox(height: 8),
                    Text(
                      'Hạn thanh toán: ${Formatters.formatDate(pendingInv.dueDate)} (Vui lòng thanh toán đúng hạn)',
                      style: AppTypography.bodySmall
                          .copyWith(color: AppColors.warningText),
                    ),
                    const Divider(height: 24),
                    Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text('Tổng thanh toán:',
                                style: AppTypography.bodySmall),
                            const SizedBox(height: 2),
                            Text(
                              Formatters.formatCurrency(pendingInv.totalAmount),
                              style: AppTypography.headingLarge
                                  .copyWith(color: AppColors.primary),
                            ),
                          ],
                        ),
                        AppButton(
                          text: 'Thanh toán ngay',
                          height: 40,
                          icon: const Icon(Icons.payment_rounded,
                              size: 18, color: Colors.white),
                          onPressed: () => _openQuickPay(pendingInv),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 24),
            ],

            // Filter Chips
            Row(
              children: [
                Text('Lịch sử hóa đơn', style: AppTypography.headingMedium),
                const Spacer(),
              ],
            ),
            const SizedBox(height: 12),
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: List.generate(_filters.length, (index) {
                  final isSelected = _selectedFilterIndex == index;
                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text(_filters[index]),
                      selected: isSelected,
                      selectedColor: AppColors.primary,
                      backgroundColor: Colors.white,
                      labelStyle: AppTypography.labelMedium.copyWith(
                        color: isSelected ? Colors.white : AppColors.textPrimary,
                      ),
                      side: BorderSide(
                        color: isSelected
                            ? AppColors.primary
                            : AppColors.border,
                      ),
                      onSelected: (selected) {
                        if (selected) {
                          setState(() {
                            _selectedFilterIndex = index;
                          });
                        }
                      },
                    ),
                  );
                }),
              ),
            ),
            const SizedBox(height: 14),

            // Invoices List
            if (_filteredInvoices.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 40),
                child: Center(
                  child: Column(
                    children: [
                      const Icon(Icons.receipt_outlined,
                          size: 48, color: AppColors.textMuted),
                      const SizedBox(height: 12),
                      Text('Không tìm thấy hóa đơn nào',
                          style: AppTypography.bodyMedium
                              .copyWith(color: AppColors.textSecondary)),
                    ],
                  ),
                ),
              )
            else
              ListView.separated(
                physics: const NeverScrollableScrollPhysics(),
                shrinkWrap: true,
                itemCount: _filteredInvoices.length,
                separatorBuilder: (context, index) =>
                    const SizedBox(height: 12),
                itemBuilder: (context, index) {
                  final inv = _filteredInvoices[index];
                  return _buildInvoiceItemCard(inv);
                },
              ),
            const SizedBox(height: 20),
          ],
        ),
      ),
    ),
  );
}

  Widget _buildInvoiceItemCard(InvoiceModel inv) {
    return AppCard(
      onTap: () => _openInvoiceDetail(inv),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: inv.isUnpaid ? AppColors.warningBg : AppColors.successBg,
                  shape: BoxShape.circle,
                ),
                child: Icon(
                  inv.isUnpaid
                      ? Icons.access_time_rounded
                      : Icons.check_circle_outline,
                  color: inv.isUnpaid ? AppColors.warning : AppColors.success,
                  size: 22,
                ),
              ),
              const SizedBox(width: 12),
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text('Hóa đơn ${inv.monthYear}',
                      style: AppTypography.labelLarge),
                  const SizedBox(height: 2),
                  Text(
                    inv.isUnpaid
                        ? 'Hạn: ${Formatters.formatDate(inv.dueDate)}'
                        : 'Đã trả: ${inv.paidDate != null ? Formatters.formatDate(inv.paidDate!) : "Đúng hạn"}',
                    style: AppTypography.bodySmall,
                  ),
                ],
              ),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.end,
            children: [
              Text(
                Formatters.formatCurrency(inv.totalAmount),
                style: AppTypography.labelLarge
                    .copyWith(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 4),
              StatusBadge(
                label: inv.status,
                tone: inv.isUnpaid ? StatusTone.amber : StatusTone.green,
              ),
            ],
          ),
        ],
      ),
    );
  }
}
