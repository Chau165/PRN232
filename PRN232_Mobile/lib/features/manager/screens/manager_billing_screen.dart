import 'package:flutter/material.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/invoice_service.dart';
import '../../../core/services/utility_reading_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';
import '../widgets/meter_record_dialog.dart';

class ManagerBillingScreen extends StatefulWidget {
  const ManagerBillingScreen({super.key});

  @override
  State<ManagerBillingScreen> createState() => _ManagerBillingScreenState();
}

class _ManagerBillingScreenState extends State<ManagerBillingScreen> {
  final UtilityReadingService _utilityService = UtilityReadingService();
  final InvoiceService _invoiceService = InvoiceService();

  int _currentTab = 0; // 0: Ghi số điện nước, 1: Hóa đơn & Thu tiền
  List<MeterReadingModel> _meterReadings = [];
  List<ManagerInvoiceModel> _invoices = [];
  bool _isLoading = false;

  int _selectedInvoiceFilterIndex = 0; // 0: Tất cả, 1: Chưa thu, 2: Đã thu, 3: Quá hạn
  final List<String> _invoiceFilters = [
    'Tất cả',
    'Chưa thu',
    'Đã thu',
    'Quá hạn',
  ];

  @override
  void initState() {
    super.initState();
    _meterReadings = AuthState.isCurrentDemo ? List.from(ManagerMockData.meterReadings) : [];
    _invoices = AuthState.isCurrentDemo ? List.from(ManagerMockData.invoices) : [];
    _loadBillingData();
  }

  Future<void> _loadBillingData() async {
    setState(() => _isLoading = true);
    try {
      final readings = await _utilityService.getReadings();
      final invoices = await _invoiceService.getManagerInvoices();
      if (mounted) {
        setState(() {
          _meterReadings = readings;
          _invoices = invoices;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  int get _recordedCount => _meterReadings.where((r) => r.isRecorded).length;

  List<ManagerInvoiceModel> get _filteredInvoices {
    if (_selectedInvoiceFilterIndex == 1) {
      return _invoices.where((i) => i.isUnpaid).toList();
    } else if (_selectedInvoiceFilterIndex == 2) {
      return _invoices.where((i) => i.isPaid).toList();
    } else if (_selectedInvoiceFilterIndex == 3) {
      return _invoices.where((i) => i.isOverdue).toList();
    }
    return _invoices;
  }

  void _openMeterRecord(MeterReadingModel reading) {
    MeterRecordSheet.show(
      context,
      reading: reading,
      onSaved: (updated) {
        setState(() {});
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Đã cập nhật chỉ số điện nước cho ${reading.roomCode}!'),
            backgroundColor: AppColors.primary,
            behavior: SnackBarBehavior.floating,
          ),
        );
      },
    );
  }

  void _collectCash(ManagerInvoiceModel invoice) async {
    setState(() {
      invoice.status = 'Paid';
      invoice.paymentMethod = 'Tiền mặt (Đã thu)';
    });
    await _invoiceService.recordCash(invoice.invoiceId);
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
            'Đã ghi nhận thu tiền mặt hóa đơn ${invoice.invoiceCode} (${invoice.roomCode})!'),
        backgroundColor: AppColors.success,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _batchGenerateInvoices() {
    final unrecorded = _meterReadings.where((r) => !r.isRecorded).length;
    if (unrecorded > 0) {
      showDialog(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('Chưa ghi đủ chỉ số'),
          content: Text(
              'Hiện vẫn còn $unrecorded phòng chưa nhập số điện nước mới. Bạn có chắc muốn chốt hóa đơn không?'),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('Quay lại ghi tiếp'),
            ),
            ElevatedButton(
              style: ElevatedButton.styleFrom(backgroundColor: AppColors.primary),
              onPressed: () {
                Navigator.pop(ctx);
                _executeBatchGenerate();
              },
              child: const Text('Vẫn tiếp tục'),
            ),
          ],
        ),
      );
    } else {
      _executeBatchGenerate();
    }
  }

  void _executeBatchGenerate() {
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Đã phát hành hóa đơn điện nước tháng 11/2026 tới tất cả khách thuê!'),
        backgroundColor: AppColors.success,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Điện Nước & Thu Chi'),
        backgroundColor: Colors.white,
        elevation: 0,
        actions: [
          if (_currentTab == 0)
            TextButton.icon(
              icon: const Icon(Icons.send_rounded, size: 16),
              label: const Text('Chốt hóa đơn'),
              style: TextButton.styleFrom(foregroundColor: AppColors.primary),
              onPressed: _batchGenerateInvoices,
            ),
        ],
      ),
      body: Column(
        children: [
          // Sub-Tab Switcher
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            color: Colors.white,
            child: Row(
              children: [
                _buildTabButton(0, 'Ghi số Điện / Nước'),
                const SizedBox(width: 8),
                _buildTabButton(1, 'Hóa đơn & Thu tiền'),
              ],
            ),
          ),
          if (_isLoading) const LinearProgressIndicator(minHeight: 2),
          const Divider(height: 1),

          Expanded(
            child: _currentTab == 0
                ? _buildMeterReadingTab()
                : _buildInvoiceManagementTab(),
          ),
        ],
      ),
    );
  }

  Widget _buildTabButton(int index, String label) {
    final isSelected = _currentTab == index;
    return Expanded(
      child: GestureDetector(
        onTap: () => setState(() => _currentTab = index),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 9),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primary : AppColors.surfaceVariant,
            borderRadius: BorderRadius.circular(10),
          ),
          alignment: Alignment.center,
          child: Text(
            label,
            style: AppTypography.labelSmall.copyWith(
              color: isSelected ? Colors.white : AppColors.textSecondary,
              fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
            ),
          ),
        ),
      ),
    );
  }

  // TAB 1: GHI CHỈ SỐ ĐIỆN NƯỚC
  Widget _buildMeterReadingTab() {
    final total = _meterReadings.length;
    final progress = _recordedCount / total;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Progress Card
        AppCard(
          color: AppColors.primaryLight,
          border: const BorderSide(color: Color(0xFFC7E7E2)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text('Tiến độ ghi số tháng 11/2026',
                      style: AppTypography.headingSmall),
                  Text('$_recordedCount/$total phòng',
                      style: AppTypography.labelMedium
                          .copyWith(color: AppColors.primary)),
                ],
              ),
              const SizedBox(height: 8),
              ClipRRect(
                borderRadius: BorderRadius.circular(6),
                child: LinearProgressIndicator(
                  value: progress,
                  minHeight: 8,
                  backgroundColor: Colors.white,
                  valueColor:
                      const AlwaysStoppedAnimation<Color>(AppColors.primary),
                ),
              ),
              const SizedBox(height: 6),
              Text(
                'Bấm vào từng phòng bên dưới để nhập số mới & chụp ảnh đồng hồ',
                style: AppTypography.bodySmall
                    .copyWith(color: AppColors.textSecondary),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // List meter readings
        ..._meterReadings.map((reading) {
          return Padding(
            padding: const EdgeInsets.only(bottom: 10),
            child: AppCard(
              onTap: () => _openMeterRecord(reading),
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: reading.isRecorded
                              ? AppColors.successBg
                              : AppColors.warningBg,
                          shape: BoxShape.circle,
                        ),
                        child: Icon(
                          reading.isRecorded
                              ? Icons.check_circle_outline_rounded
                              : Icons.edit_note_rounded,
                          color: reading.isRecorded
                              ? AppColors.success
                              : AppColors.warning,
                          size: 22,
                        ),
                      ),
                      const SizedBox(width: 12),
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Row(
                            children: [
                              Text(reading.roomCode,
                                  style: AppTypography.headingSmall),
                              const SizedBox(width: 6),
                              Text('• ${reading.floor}',
                                  style: AppTypography.bodySmall),
                            ],
                          ),
                          const SizedBox(height: 2),
                          if (reading.isRecorded) ...[
                            Text(
                              'Điện: ${reading.newElectricity} (dùng ${reading.electricityUsage} kWh) • Nước: ${reading.newWater} (dùng ${reading.waterUsage} m³)',
                              style: AppTypography.bodySmall.copyWith(
                                color: AppColors.textSecondary,
                                fontSize: 11,
                              ),
                            ),
                          ] else ...[
                            Text(
                              'Số cũ: Điện ${reading.oldElectricity} • Nước ${reading.oldWater}',
                              style: AppTypography.bodySmall
                                  .copyWith(color: AppColors.textMuted),
                            ),
                          ],
                        ],
                      ),
                    ],
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      StatusBadge(
                        label: reading.isRecorded ? 'Đã ghi' : 'Chưa ghi',
                        tone: reading.isRecorded
                            ? StatusTone.green
                            : StatusTone.amber,
                      ),
                      const SizedBox(height: 4),
                      const Icon(Icons.arrow_forward_ios_rounded,
                          size: 14, color: AppColors.textMuted),
                    ],
                  ),
                ],
              ),
            ),
          );
        }),
      ],
    );
  }

  // TAB 2: HÓA ĐƠN & THU TIỀN
  Widget _buildInvoiceManagementTab() {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        // Summary
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
                    Text('3/6 phòng đã trả',
                        style: AppTypography.bodySmall.copyWith(fontSize: 11)),
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
                    Text('11.8 tr',
                        style: AppTypography.headingLarge
                            .copyWith(color: AppColors.warning)),
                    Text('3 phòng chờ thu',
                        style: AppTypography.bodySmall.copyWith(fontSize: 11)),
                  ],
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 16),

        // Filter Chips
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: List.generate(_invoiceFilters.length, (index) {
              final isSelected = _selectedInvoiceFilterIndex == index;
              return Padding(
                padding: const EdgeInsets.only(right: 8),
                child: ChoiceChip(
                  label: Text(_invoiceFilters[index]),
                  selected: isSelected,
                  selectedColor: AppColors.primary,
                  backgroundColor: AppColors.surfaceVariant,
                  labelStyle: AppTypography.labelSmall.copyWith(
                    color: isSelected ? Colors.white : AppColors.textPrimary,
                    fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                  ),
                  side: BorderSide(
                    color: isSelected ? AppColors.primary : AppColors.border,
                  ),
                  onSelected: (selected) {
                    if (selected) {
                      setState(() => _selectedInvoiceFilterIndex = index);
                    }
                  },
                ),
              );
            }),
          ),
        ),
        const SizedBox(height: 14),

        // Invoices list
        ..._filteredInvoices.map((inv) {
          return Padding(
            padding: const EdgeInsets.only(bottom: 12),
            child: AppCard(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Row(
                        children: [
                          Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 8, vertical: 4),
                            decoration: BoxDecoration(
                              color: AppColors.primaryLight,
                              borderRadius: BorderRadius.circular(6),
                            ),
                            child: Text(inv.roomCode,
                                style: AppTypography.labelSmall
                                    .copyWith(color: AppColors.primary)),
                          ),
                          const SizedBox(width: 10),
                          Text(inv.tenantName,
                              style: AppTypography.headingSmall),
                        ],
                      ),
                      StatusBadge(label: inv.status),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Mã: ${inv.invoiceCode} • ${inv.monthYear}',
                          style: AppTypography.bodySmall),
                      Text(
                        Formatters.formatCurrency(inv.amount),
                        style: AppTypography.labelLarge
                            .copyWith(fontWeight: FontWeight.bold),
                      ),
                    ],
                  ),
                  if (inv.paymentMethod != null) ...[
                    const SizedBox(height: 4),
                    Text('Phương thức: ${inv.paymentMethod}',
                        style: AppTypography.bodySmall
                            .copyWith(color: AppColors.textMuted)),
                  ],
                  if (inv.isUnpaid || inv.isOverdue) ...[
                    const Divider(height: 18),
                    Row(
                      children: [
                        Expanded(
                          child: AppButton(
                            text: 'Xác nhận đã thu tiền mặt',
                            height: 36,
                            icon: const Icon(Icons.payments_outlined,
                                size: 16, color: Colors.white),
                            onPressed: () => _collectCash(inv),
                          ),
                        ),
                      ],
                    ),
                  ],
                ],
              ),
            ),
          );
        }),
      ],
    );
  }
}
