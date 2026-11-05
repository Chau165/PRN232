import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_text_field.dart';
import '../models/customer_models.dart';

class InvoiceComplaintBottomSheet extends StatefulWidget {
  final InvoiceModel invoice;

  const InvoiceComplaintBottomSheet({
    super.key,
    required this.invoice,
  });

  static Future<void> show(BuildContext context, InvoiceModel invoice) {
    return AppBottomSheet.show(
      context: context,
      title: 'Khiếu nại chỉ số hóa đơn',
      subtitle: '${invoice.invoiceCode} • ${invoice.monthYear}',
      child: InvoiceComplaintBottomSheet(invoice: invoice),
    );
  }

  @override
  State<InvoiceComplaintBottomSheet> createState() =>
      _InvoiceComplaintBottomSheetState();
}

class _InvoiceComplaintBottomSheetState
    extends State<InvoiceComplaintBottomSheet> {
  String _selectedReasonType = 'Sai chỉ số điện sinh hoạt';
  final _readingController = TextEditingController();
  final _noteController = TextEditingController();
  bool _isSubmitting = false;

  final List<String> _reasonTypes = [
    'Sai chỉ số điện sinh hoạt',
    'Sai chỉ số nước sinh hoạt',
    'Chi phí dịch vụ không chính xác',
    'Lý do khác',
  ];

  @override
  void dispose() {
    _readingController.dispose();
    _noteController.dispose();
    super.dispose();
  }

  void _submit() async {
    if (_noteController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
            content: Text('Vui lòng nhập chi tiết phản ánh sai sót.')),
      );
      return;
    }

    setState(() => _isSubmitting = true);
    await Future.delayed(const Duration(milliseconds: 700));
    if (!mounted) return;
    setState(() => _isSubmitting = false);

    Navigator.pop(context);
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        backgroundColor: AppColors.success,
        content: Text(
            'Khiếu nại đã được gửi! Quản lý sẽ kiểm tra lại chỉ số và phản hồi sớm.'),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text('1. Khoản mục cần khiếu nại', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: AppColors.border),
          ),
          child: DropdownButtonHideUnderline(
            child: DropdownButton<String>(
              value: _selectedReasonType,
              isExpanded: true,
              items: _reasonTypes.map((type) {
                return DropdownMenuItem<String>(
                  value: type,
                  child: Text(type, style: AppTypography.bodyMedium),
                );
              }).toList(),
              onChanged: (val) {
                if (val != null) setState(() => _selectedReasonType = val);
              },
            ),
          ),
        ),
        const SizedBox(height: 16),

        AppTextField(
          label: 'Chỉ số thực tế bạn kiểm tra được (nếu có)',
          hint: 'vd: Đồng hồ điện tại phòng chỉ mới 1.450 kWh',
          controller: _readingController,
        ),
        const SizedBox(height: 14),

        AppTextField(
          label: 'Nội dung phản ánh chi tiết',
          hint: 'Mô tả rõ lý do hoặc sai lệch bạn phát hiện...',
          controller: _noteController,
          maxLines: 3,
        ),
        const SizedBox(height: 24),

        AppButton(
          text: 'Gửi khiếu nại đến Quản lý',
          isLoading: _isSubmitting,
          icon: const Icon(Icons.send_rounded, size: 18, color: Colors.white),
          onPressed: _submit,
        ),
        const SizedBox(height: 10),
      ],
    );
  }
}
