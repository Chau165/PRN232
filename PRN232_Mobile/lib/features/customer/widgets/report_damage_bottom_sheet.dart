import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import '../../../core/auth/auth_state.dart';
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
  final List<EquipmentItem>? equipments;

  const ReportDamageBottomSheet({
    super.key,
    this.contractId,
    this.roomCode,
    this.equipments,
  });

  static Future<bool?> show(
    BuildContext context, {
    int? contractId,
    String? roomCode,
    List<EquipmentItem>? equipments,
  }) {
    return AppBottomSheet.show<bool>(
      context: context,
      title: 'Báo hỏng / Yêu cầu sửa chữa',
      subtitle: roomCode != null ? 'Phòng $roomCode' : 'Phòng đang thuê',
      child: ReportDamageBottomSheet(
        contractId: contractId,
        roomCode: roomCode,
        equipments: equipments,
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
  List<EquipmentItem> _availableEquipments = [];
  EquipmentItem? _selectedEquipment;
  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    if (widget.equipments != null && widget.equipments!.isNotEmpty) {
      _availableEquipments = widget.equipments!;
    } else if (AuthState.isCurrentDemo) {
      _availableEquipments = CustomerMockData.myRoomEquipments;
    } else {
      _availableEquipments = [];
    }

    if (_availableEquipments.isNotEmpty) {
      _selectedEquipment = _availableEquipments.first;
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

    final effectiveContractId = widget.contractId ?? (AuthState.isCurrentDemo ? 1 : null);
    if (effectiveContractId == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Không tìm thấy hợp đồng phòng thuê hoạt động.')),
      );
      return;
    }

    setState(() => _isSubmitting = true);
    try {
      await _maintenanceService.createRequest(
        contractId: effectiveContractId,
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
        // Equipment Selection Dropdown (nếu có thiết bị)
        if (_availableEquipments.isNotEmpty) ...[
          Text('Thiết bị gặp sự cố', style: AppTypography.labelLarge),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
            decoration: BoxDecoration(
              color: AppColors.surfaceVariant,
              borderRadius: BorderRadius.circular(12),
              border: Border.all(color: AppColors.border),
            ),
            child: DropdownButtonHideUnderline(
              child: DropdownButton<EquipmentItem>(
                value: _selectedEquipment,
                isExpanded: true,
                items: _availableEquipments.map((eq) {
                  return DropdownMenuItem<EquipmentItem>(
                    value: eq,
                    child: Text('${eq.name} (${eq.code})',
                        style: AppTypography.bodyMedium),
                  );
                }).toList(),
                onChanged: (val) => setState(() => _selectedEquipment = val),
              ),
            ),
          ),
          const SizedBox(height: 16),
        ],

        // Description Input
        AppTextField(
          label: 'Mô tả chi tiết sự cố',
          hint: 'Ví dụ: Máy lạnh chảy nước, bóng đèn phòng khách bị chập...',
          controller: _descriptionController,
          maxLines: 4,
        ),
        const SizedBox(height: 16),

        // Photo Upload Picker
        Text('Ảnh chụp hiện trường sự cố', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            ..._selectedImages.map((img) {
              return Stack(
                children: [
                  ClipRRect(
                    borderRadius: BorderRadius.circular(8),
                    child: Image.file(
                      File(img.path),
                      width: 72,
                      height: 72,
                      fit: BoxFit.cover,
                    ),
                  ),
                  Positioned(
                    top: 2,
                    right: 2,
                    child: GestureDetector(
                      onTap: () => setState(() => _selectedImages.remove(img)),
                      child: Container(
                        decoration: const BoxDecoration(
                          color: Colors.black54,
                          shape: BoxShape.circle,
                        ),
                        padding: const EdgeInsets.all(2),
                        child: const Icon(Icons.close,
                            size: 14, color: Colors.white),
                      ),
                    ),
                  ),
                ],
              );
            }),
            InkWell(
              onTap: () => _pickImage(ImageSource.camera),
              borderRadius: BorderRadius.circular(8),
              child: Container(
                width: 72,
                height: 72,
                decoration: BoxDecoration(
                  border: Border.all(
                      color: AppColors.primary, style: BorderStyle.solid),
                  borderRadius: BorderRadius.circular(8),
                  color: AppColors.primaryLight,
                ),
                child: const Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.camera_alt_outlined,
                        color: AppColors.primary, size: 24),
                    SizedBox(height: 2),
                    Text('Chụp ảnh',
                        style: TextStyle(
                            color: AppColors.primary,
                            fontSize: 10,
                            fontWeight: FontWeight.bold)),
                  ],
                ),
              ),
            ),
            InkWell(
              onTap: () => _pickImage(ImageSource.gallery),
              borderRadius: BorderRadius.circular(8),
              child: Container(
                width: 72,
                height: 72,
                decoration: BoxDecoration(
                  border: Border.all(color: AppColors.border),
                  borderRadius: BorderRadius.circular(8),
                  color: AppColors.surfaceVariant,
                ),
                child: const Column(
                  mainAxisAlignment: MainAxisAlignment.center,
                  children: [
                    Icon(Icons.photo_library_outlined,
                        color: AppColors.textSecondary, size: 24),
                    SizedBox(height: 2),
                    Text('Thư viện',
                        style: TextStyle(
                            color: AppColors.textSecondary,
                            fontSize: 10,
                            fontWeight: FontWeight.w500)),
                  ],
                ),
              ),
            ),
          ],
        ),
        const SizedBox(height: 24),

        // Submit Button
        AppButton(
          text: 'Gửi yêu cầu sửa chữa',
          isLoading: _isSubmitting,
          onPressed: _submit,
        ),
      ],
    );
  }
}
