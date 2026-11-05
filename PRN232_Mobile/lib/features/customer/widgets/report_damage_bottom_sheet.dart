import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_text_field.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';

class ReportDamageBottomSheet extends StatefulWidget {
  const ReportDamageBottomSheet({super.key});

  static Future<bool?> show(BuildContext context) {
    return AppBottomSheet.show<bool>(
      context: context,
      title: 'Báo hỏng / Yêu cầu sửa chữa',
      subtitle: 'Phòng 204 • Nhà trọ Bình An',
      child: const ReportDamageBottomSheet(),
    );
  }

  @override
  State<ReportDamageBottomSheet> createState() =>
      _ReportDamageBottomSheetState();
}

class _ReportDamageBottomSheetState extends State<ReportDamageBottomSheet> {
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
      final XFile? file = await _picker.pickImage(
        source: source,
        maxWidth: 1024,
        maxHeight: 1024,
        imageQuality: 85,
      );
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
    await Future.delayed(const Duration(milliseconds: 900));
    if (!mounted) return;
    setState(() => _isSubmitting = false);
    Navigator.pop(context, true);
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
        Text('2. Mô tả hiện tượng hư hỏng', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        AppTextField(
          hint: 'Mô tả chi tiết (vd: Máy lạnh kêu to và chảy nước ở máng xả...)',
          controller: _descriptionController,
          maxLines: 3,
        ),
        const SizedBox(height: 18),

        // Photo Upload / Camera
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            Text('3. Hình ảnh chụp sự cố thực tế',
                style: AppTypography.labelLarge),
            Text('${_selectedImages.length}/3 ảnh',
                style: AppTypography.bodySmall),
          ],
        ),
        const SizedBox(height: 10),

        Row(
          children: [
            // Camera Button
            Expanded(
              child: OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size.fromHeight(42),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                  side: const BorderSide(color: AppColors.primary),
                ),
                icon: const Icon(Icons.camera_alt_rounded, size: 18),
                label: const Text('Chụp ảnh'),
                onPressed: _selectedImages.length >= 3
                    ? null
                    : () => _pickImage(ImageSource.camera),
              ),
            ),
            const SizedBox(width: 10),
            // Gallery Button
            Expanded(
              child: OutlinedButton.icon(
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size.fromHeight(42),
                  shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10)),
                  side: const BorderSide(color: AppColors.primary),
                ),
                icon: const Icon(Icons.photo_library_outlined, size: 18),
                label: const Text('Thư viện'),
                onPressed: _selectedImages.length >= 3
                    ? null
                    : () => _pickImage(ImageSource.gallery),
              ),
            ),
          ],
        ),
        const SizedBox(height: 12),

        // Thumbnail Previews
        if (_selectedImages.isNotEmpty)
          SizedBox(
            height: 80,
            child: ListView.separated(
              scrollDirection: Axis.horizontal,
              itemCount: _selectedImages.length,
              separatorBuilder: (_, _) => const SizedBox(width: 10),
              itemBuilder: (context, index) {
                final file = _selectedImages[index];
                return Stack(
                  children: [
                    ClipRRect(
                      borderRadius: BorderRadius.circular(10),
                      child: Image.file(
                        File(file.path),
                        width: 80,
                        height: 80,
                        fit: BoxFit.cover,
                      ),
                    ),
                    Positioned(
                      top: 4,
                      right: 4,
                      child: GestureDetector(
                        onTap: () {
                          setState(() => _selectedImages.removeAt(index));
                        },
                        child: Container(
                          padding: const EdgeInsets.all(2),
                          decoration: const BoxDecoration(
                            color: Colors.black54,
                            shape: BoxShape.circle,
                          ),
                          child: const Icon(Icons.close,
                              size: 14, color: Colors.white),
                        ),
                      ),
                    ),
                  ],
                );
              },
            ),
          ),
        const SizedBox(height: 24),

        AppButton(
          text: 'Gửi phiếu báo hỏng',
          isLoading: _isSubmitting,
          icon: const Icon(Icons.send_rounded, size: 18, color: Colors.white),
          onPressed: _submit,
        ),
        const SizedBox(height: 8),
      ],
    );
  }
}
