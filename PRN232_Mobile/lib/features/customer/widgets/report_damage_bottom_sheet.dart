import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/maintenance_service.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_text_field.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';

class ReportDamageBottomSheet extends StatefulWidget {
  final int? contractId;
  final String? roomCode;

  const ReportDamageBottomSheet({
    super.key,
    this.contractId,
    this.roomCode,
  });

  static Future<bool?> show(
    BuildContext context, {
    int? contractId,
    String? roomCode,
  }) {
    return AppBottomSheet.show<bool>(
      context: context,
      title: 'Báo hỏng / Yêu cầu sửa chữa',
      subtitle: roomCode != null ? 'Phòng $roomCode' : 'Phòng đang thuê',
      child: ReportDamageBottomSheet(
        contractId: contractId,
        roomCode: roomCode,
      ),
    );
  }

  @override
  State<ReportDamageBottomSheet> createState() =>
      _ReportDamageBottomSheetState();
}

class _ReportDamageBottomSheetState extends State<ReportDamageBottomSheet> {
  final MaintenanceService _maintenanceService = MaintenanceService();
  final ImagePicker _picker = ImagePicker();
  final List<XFile> _selectedImages = [];
  final _descriptionController = TextEditingController();
  EquipmentItem? _selectedEquipment;
  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    if (CustomerMockData.myRoomEquipments.isNotEmpty) {
      _selectedEquipment = CustomerMockData.myRoomEquipments.first;
    }
  }

  @override
  void dispose() {
    _descriptionController.dispose();
    super.dispose();
  }

  Future<void> _pickImage(ImageSource source) async {
    try {
      final file = await _picker.pickImage(source: source, imageQuality: 75);
      if (file != null) {
        setState(() {
          _selectedImages.add(file);
        });
      }
    } catch (_) {
      // Bỏ qua lỗi hủy picker
    }
  }

  void _submit() async {
    if (_descriptionController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng mô tả chi tiết sự cố hư hỏng.')),
      );
      return;
    }

    setState(() => _isSubmitting = true);
    try {
      await _maintenanceService.createRequest(
        contractId: widget.contractId ?? 1,
        equipmentId: _selectedEquipment?.id,
        description: _descriptionController.text.trim(),
      );
      if (!mounted) return;
      setState(() => _isSubmitting = false);
      Navigator.pop(context, true);
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
        // Equipment dropdown
        Text('1. Chọn thiết bị gặp sự cố', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 14),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: AppColors.border),
          ),
          child: DropdownButtonHideUnderline(
            child: DropdownButton<EquipmentItem>(
              value: _selectedEquipment,
              isExpanded: true,
              icon: const Icon(Icons.arrow_drop_down_rounded,
                  color: AppColors.primary),
              items: CustomerMockData.myRoomEquipments.map((eq) {
                return DropdownMenuItem<EquipmentItem>(
                  value: eq,
                  child: Text(
                    '${eq.name} (${eq.code})',
                    style: AppTypography.bodyMedium,
                  ),
                );
              }).toList(),
              onChanged: (val) {
                if (val != null) setState(() => _selectedEquipment = val);
              },
            ),
          ),
        ),
        const SizedBox(height: 18),

        // Description
        AppTextField(
          label: '2. Mô tả hiện trạng hư hỏng',
          controller: _descriptionController,
          hint: 'Ví dụ: Máy lạnh không mát, kêu to và bị chảy nước...',
          maxLines: 3,
        ),
        const SizedBox(height: 18),

        // Photos
        Text('3. Hình ảnh chụp thực tế (tùy chọn)',
            style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        SingleChildScrollView(
          scrollDirection: Axis.horizontal,
          child: Row(
            children: [
              ..._selectedImages.map((img) {
                return Stack(
                  children: [
                    Container(
                      width: 80,
                      height: 80,
                      margin: const EdgeInsets.only(right: 8),
                      decoration: BoxDecoration(
                        borderRadius: BorderRadius.circular(12),
                        image: DecorationImage(
                          image: FileImage(File(img.path)),
                          fit: BoxFit.cover,
                        ),
                      ),
                    ),
                    Positioned(
                      top: 4,
                      right: 12,
                      child: GestureDetector(
                        onTap: () {
                          setState(() => _selectedImages.remove(img));
                        },
                        child: Container(
                          padding: const EdgeInsets.all(2),
                          decoration: const BoxDecoration(
                            color: Colors.black54,
                            shape: BoxShape.circle,
                          ),
                          child: const Icon(Icons.close_rounded,
                              size: 14, color: Colors.white),
                        ),
                      ),
                    ),
                  ],
                );
              }),
              InkWell(
                onTap: () => _pickImage(ImageSource.camera),
                borderRadius: BorderRadius.circular(12),
                child: Container(
                  width: 80,
                  height: 80,
                  margin: const EdgeInsets.only(right: 8),
                  decoration: BoxDecoration(
                    color: AppColors.surfaceVariant,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(
                        color: AppColors.border, style: BorderStyle.solid),
                  ),
                  child: const Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.camera_alt_outlined,
                          size: 22, color: AppColors.primary),
                      SizedBox(height: 4),
                      Text('Chụp ảnh',
                          style: TextStyle(
                              fontSize: 10, color: AppColors.textSecondary)),
                    ],
                  ),
                ),
              ),
              InkWell(
                onTap: () => _pickImage(ImageSource.gallery),
                borderRadius: BorderRadius.circular(12),
                child: Container(
                  width: 80,
                  height: 80,
                  decoration: BoxDecoration(
                    color: AppColors.surfaceVariant,
                    borderRadius: BorderRadius.circular(12),
                    border: Border.all(
                        color: AppColors.border, style: BorderStyle.solid),
                  ),
                  child: const Column(
                    mainAxisAlignment: MainAxisAlignment.center,
                    children: [
                      Icon(Icons.photo_library_outlined,
                          size: 22, color: AppColors.primary),
                      SizedBox(height: 4),
                      Text('Thư viện',
                          style: TextStyle(
                              fontSize: 10, color: AppColors.textSecondary)),
                    ],
                  ),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),

        // Submit Button
        AppButton(
          text: 'Gửi yêu cầu sửa chữa',
          isLoading: _isSubmitting,
          onPressed: _submit,
        ),
        const SizedBox(height: 12),
      ],
    );
  }
}
