import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/app_text_field.dart';
import '../models/manager_models.dart';

class MaintenanceReportSheet extends StatefulWidget {
  final ManagerTaskModel task;

  const MaintenanceReportSheet({
    super.key,
    required this.task,
  });

  static Future<bool?> show(
    BuildContext context, {
    required ManagerTaskModel task,
  }) {
    return AppBottomSheet.show<bool>(
      context: context,
      title: 'Nghiệm thu bảo trì',
      subtitle: '${task.roomCode} • ${task.title}',
      child: MaintenanceReportSheet(task: task),
    );
  }

  @override
  State<MaintenanceReportSheet> createState() => _MaintenanceReportSheetState();
}

class _MaintenanceReportSheetState extends State<MaintenanceReportSheet> {
  final ImagePicker _picker = ImagePicker();
  final TextEditingController _costController = TextEditingController(text: '150000');
  final TextEditingController _notesController = TextEditingController(
    text: 'Đã thay mới gioăng cao su và van khóa. Kiểm tra áp lực nước hoạt động tốt.',
  );
  XFile? _receiptImage;
  bool _isSubmitting = false;

  @override
  void dispose() {
    _costController.dispose();
    _notesController.dispose();
    super.dispose();
  }

  Future<void> _pickReceipt(ImageSource source) async {
    final file = await _picker.pickImage(
      source: source,
      maxWidth: 1024,
      maxHeight: 1024,
      imageQuality: 85,
    );
    if (file != null) {
      setState(() => _receiptImage = file);
    }
  }

  void _submit() async {
    final cost = num.tryParse(_costController.text.trim()) ?? 0;

    setState(() => _isSubmitting = true);
    await Future.delayed(const Duration(milliseconds: 600));
    if (!mounted) return;

    widget.task.status = TaskStatus.completed;
    widget.task.actualCost = cost;
    widget.task.completionNotes = _notesController.text.trim();
    if (_receiptImage != null) {
      widget.task.invoiceReceiptImage = _receiptImage!.path;
    }

    Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Problem summary
        AppCard(
          color: AppColors.primaryLight,
          border: const BorderSide(color: Color(0xFFC7E7E2)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(widget.task.title, style: AppTypography.headingSmall),
              const SizedBox(height: 4),
              Text('${widget.task.roomCode} • Thợ: ${widget.task.technicianName ?? "Nguyễn Văn Nam"}',
                  style: AppTypography.bodySmall),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // Cost
        AppTextField(
          label: 'Tổng chi phí thực tế (VNĐ)',
          hint: '150000',
          controller: _costController,
          keyboardType: TextInputType.number,
        ),
        const SizedBox(height: 14),

        // Notes
        AppTextField(
          label: 'Biên bản nghiệm thu & ghi chú',
          hint: 'Mô tả kết quả sau khi kiểm tra...',
          controller: _notesController,
          maxLines: 2,
        ),
        const SizedBox(height: 16),

        // Receipt image
        Text('Ảnh hóa đơn / ảnh nghiệm thu hoàn tất', style: AppTypography.headingSmall),
        const SizedBox(height: 8),
        if (_receiptImage != null)
          Stack(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(10),
                child: Image.file(
                  File(_receiptImage!.path),
                  height: 140,
                  width: double.infinity,
                  fit: BoxFit.cover,
                ),
              ),
              Positioned(
                top: 6,
                right: 6,
                child: CircleAvatar(
                  backgroundColor: Colors.black54,
                  radius: 14,
                  child: IconButton(
                    padding: EdgeInsets.zero,
                    icon: const Icon(Icons.close, color: Colors.white, size: 16),
                    onPressed: () => setState(() => _receiptImage = null),
                  ),
                ),
              ),
            ],
          )
        else
          Row(
            children: [
              Expanded(
                child: OutlinedButton.icon(
                  icon: const Icon(Icons.camera_alt_outlined, size: 18),
                  label: const Text('Chụp hóa đơn'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.primary,
                    side: const BorderSide(color: AppColors.primary),
                    padding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                  onPressed: () => _pickReceipt(ImageSource.camera),
                ),
              ),
              const SizedBox(width: 10),
              Expanded(
                child: OutlinedButton.icon(
                  icon: const Icon(Icons.photo_library_outlined, size: 18),
                  label: const Text('Thư viện'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.textSecondary,
                    side: const BorderSide(color: AppColors.border),
                    padding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                  onPressed: () => _pickReceipt(ImageSource.gallery),
                ),
              ),
            ],
          ),
        const SizedBox(height: 24),

        AppButton(
          text: 'Xác nhận nghiệm thu & Đóng sự cố',
          isLoading: _isSubmitting,
          onPressed: _submit,
        ),
        const SizedBox(height: 10),
      ],
    );
  }
}
