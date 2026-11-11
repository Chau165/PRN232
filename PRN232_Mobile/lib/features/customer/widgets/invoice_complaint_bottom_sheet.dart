import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/invoice_service.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_text_field.dart';
import '../models/customer_models.dart';

class InvoiceComplaintBottomSheet extends StatefulWidget {
  final InvoiceModel invoice;

  const InvoiceComplaintBottomSheet({super.key, required this.invoice});

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
  final InvoiceService _invoiceService = InvoiceService();
  String _selectedReasonType = 'Điện';
  final _readingController = TextEditingController();
  final _noteController = TextEditingController();
  bool _isSubmitting = false;

  final List<String> _reasonTypes = [
    'Điện',
    'Nước',
    'Dịch vụ',
    'Khác',
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
    final readingText = _readingController.text.trim().isNotEmpty
        ? ' [Chỉ số đồng hồ thực tế chụp được: ${_readingController.text.trim()}]'
        : '';
    final fullContent = '${_noteController.text.trim()}$readingText';

    try {
      await _invoiceService.createComplaint(
        invoiceId: widget.invoice.invoiceId,
        feeType: _selectedReasonType,
        content: fullContent,
      );

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
    } catch (e) {
      if (!mounted) return;
      setState(() => _isSubmitting = false);
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
        Text(
          'Nếu bạn thấy tiền điện, nước hoặc dịch vụ tháng này có sự chênh lệch bất thường so với thực tế sử dụng, vui lòng gửi phản ánh dưới đây.',
          style: AppTypography.bodySmall,
        ),
        const SizedBox(height: 16),

        // Reason type
        Text('Khoản mục cần khiếu nại', style: AppTypography.labelLarge),
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
        const SizedBox(height: 14),

        // Actual meter reading
        AppTextField(
          label: 'Chỉ số thực tế trên đồng hồ của bạn (nếu có)',
          controller: _readingController,
          hint: 'Ví dụ: Số điện hiện tại là 1450',
          keyboardType: TextInputType.number,
        ),
        const SizedBox(height: 14),

        // Note
        AppTextField(
          label: 'Nội dung phản ánh chi tiết *',
          controller: _noteController,
          hint:
              'Mô tả chi tiết sai lệch hoặc lý do bạn thấy hoá đơn không khớp...',
          maxLines: 3,
        ),
        const SizedBox(height: 24),

        AppButton(
          text: 'Gửi khiếu nại cho Quản lý',
          isLoading: _isSubmitting,
          onPressed: _submit,
        ),
        const SizedBox(height: 12),
      ],
    );
  }
}
