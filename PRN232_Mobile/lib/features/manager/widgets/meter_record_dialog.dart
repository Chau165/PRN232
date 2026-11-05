import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/app_text_field.dart';
import '../models/manager_models.dart';

class MeterRecordSheet extends StatefulWidget {
  final MeterReadingModel reading;
  final ValueChanged<MeterReadingModel>? onSaved;

  const MeterRecordSheet({
    super.key,
    required this.reading,
    this.onSaved,
  });

  static Future<MeterReadingModel?> show(
    BuildContext context, {
    required MeterReadingModel reading,
    ValueChanged<MeterReadingModel>? onSaved,
  }) {
    return AppBottomSheet.show<MeterReadingModel>(
      context: context,
      title: 'Ghi chỉ số ${reading.roomCode}',
      subtitle: '${reading.floor} • Tháng 11/2026',
      child: MeterRecordSheet(
        reading: reading,
        onSaved: onSaved,
      ),
    );
  }

  @override
  State<MeterRecordSheet> createState() => _MeterRecordSheetState();
}

class _MeterRecordSheetState extends State<MeterRecordSheet> {
  final ImagePicker _picker = ImagePicker();
  late TextEditingController _elecController;
  late TextEditingController _waterController;
  XFile? _meterImage;

  num _electricityUsage = 0;
  num _waterUsage = 0;

  @override
  void initState() {
    super.initState();
    _elecController = TextEditingController(
      text: widget.reading.newElectricity != null
          ? widget.reading.newElectricity.toString()
          : '',
    );
    _waterController = TextEditingController(
      text: widget.reading.newWater != null
          ? widget.reading.newWater.toString()
          : '',
    );

    _calculateUsage();
  }

  @override
  void dispose() {
    _elecController.dispose();
    _waterController.dispose();
    super.dispose();
  }

  void _calculateUsage() {
    final newE = num.tryParse(_elecController.text) ?? 0;
    final newW = num.tryParse(_waterController.text) ?? 0;

    setState(() {
      _electricityUsage = (newE >= widget.reading.oldElectricity)
          ? (newE - widget.reading.oldElectricity)
          : 0;
      _waterUsage = (newW >= widget.reading.oldWater)
          ? (newW - widget.reading.oldWater)
          : 0;
    });
  }

  Future<void> _pickImage(ImageSource source) async {
    final XFile? file = await _picker.pickImage(
      source: source,
      maxWidth: 1024,
      maxHeight: 1024,
      imageQuality: 85,
    );
    if (file != null) {
      setState(() => _meterImage = file);
    }
  }

  void _save() {
    final newE = num.tryParse(_elecController.text);
    final newW = num.tryParse(_waterController.text);

    if (newE == null || newE < widget.reading.oldElectricity) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Số điện mới phải lớn hơn hoặc bằng số điện cũ.'),
        ),
      );
      return;
    }

    if (newW == null || newW < widget.reading.oldWater) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Số nước mới phải lớn hơn hoặc bằng số nước cũ.'),
        ),
      );
      return;
    }

    widget.reading.newElectricity = newE;
    widget.reading.newWater = newW;
    widget.reading.isRecorded = true;
    widget.reading.recordDate = DateTime.now();
    if (_meterImage != null) {
      widget.reading.meterImage = _meterImage!.path;
    }

    widget.onSaved?.call(widget.reading);
    Navigator.pop(context, widget.reading);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Electric Reading Card
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: const Color(0xFFFEF3C7),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Icon(Icons.bolt_rounded,
                        color: Color(0xFFD97706), size: 20),
                  ),
                  const SizedBox(width: 10),
                  Text('Chỉ số ĐIỆN (kWh)', style: AppTypography.headingSmall),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Số cũ', style: AppTypography.bodySmall),
                        const SizedBox(height: 4),
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 14, vertical: 12),
                          decoration: BoxDecoration(
                            color: AppColors.surfaceVariant,
                            borderRadius: BorderRadius.circular(8),
                            border: Border.all(color: AppColors.border),
                          ),
                          child: Text(
                            '${widget.reading.oldElectricity}',
                            style: AppTypography.labelLarge,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: AppTextField(
                      label: 'Số mới',
                      hint: 'Nhập số mới',
                      controller: _elecController,
                      keyboardType: TextInputType.number,
                      onChanged: (_) => _calculateUsage(),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                'Tiêu thụ: $_electricityUsage kWh (× 3.500 ₫ = ${Formatters.formatCurrency(_electricityUsage * 3500)})',
                style: AppTypography.bodySmall.copyWith(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 14),

        // Water Reading Card
        AppCard(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: const Color(0xFFE0F2FE),
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Icon(Icons.water_drop_rounded,
                        color: Color(0xFF0284C7), size: 20),
                  ),
                  const SizedBox(width: 10),
                  Text('Chỉ số NƯỚC (m³)', style: AppTypography.headingSmall),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Số cũ', style: AppTypography.bodySmall),
                        const SizedBox(height: 4),
                        Container(
                          padding: const EdgeInsets.symmetric(
                              horizontal: 14, vertical: 12),
                          decoration: BoxDecoration(
                            color: AppColors.surfaceVariant,
                            borderRadius: BorderRadius.circular(8),
                            border: Border.all(color: AppColors.border),
                          ),
                          child: Text(
                            '${widget.reading.oldWater}',
                            style: AppTypography.labelLarge,
                          ),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: AppTextField(
                      label: 'Số mới',
                      hint: 'Nhập số mới',
                      controller: _waterController,
                      keyboardType: TextInputType.number,
                      onChanged: (_) => _calculateUsage(),
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                'Tiêu thụ: $_waterUsage m³ (× 20.000 ₫ = ${Formatters.formatCurrency(_waterUsage * 20000)})',
                style: AppTypography.bodySmall.copyWith(
                  color: AppColors.primary,
                  fontWeight: FontWeight.w600,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // Meter Photo Proof
        Text('Ảnh đồng hồ công tơ (Đối soát tranh chấp)',
            style: AppTypography.headingSmall),
        const SizedBox(height: 8),
        if (_meterImage != null)
          Stack(
            children: [
              ClipRRect(
                borderRadius: BorderRadius.circular(10),
                child: Image.file(
                  File(_meterImage!.path),
                  height: 120,
                  width: double.infinity,
                  fit: BoxFit.cover,
                ),
              ),
              Positioned(
                top: 6,
                right: 6,
                child: CircleAvatar(
                  radius: 14,
                  backgroundColor: Colors.black54,
                  child: IconButton(
                    padding: EdgeInsets.zero,
                    icon: const Icon(Icons.close, size: 16, color: Colors.white),
                    onPressed: () => setState(() => _meterImage = null),
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
                  label: const Text('Chụp ảnh'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.primary,
                    side: const BorderSide(color: AppColors.primary),
                    padding: const EdgeInsets.symmetric(vertical: 12),
                  ),
                  onPressed: () => _pickImage(ImageSource.camera),
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
                  onPressed: () => _pickImage(ImageSource.gallery),
                ),
              ),
            ],
          ),
        const SizedBox(height: 24),

        AppButton(
          text: 'Lưu chỉ số ${widget.reading.roomCode}',
          onPressed: _save,
        ),
        const SizedBox(height: 10),
      ],
    );
  }
}
