import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_text_field.dart';
import '../models/customer_models.dart';

class ViewingRequestBottomSheet extends StatefulWidget {
  final PropertyItem property;
  final RoomItem? room;

  const ViewingRequestBottomSheet({
    super.key,
    required this.property,
    this.room,
  });

  static Future<bool?> show(
    BuildContext context, {
    required PropertyItem property,
    RoomItem? room,
  }) {
    return AppBottomSheet.show<bool>(
      context: context,
      title: 'Đặt lịch xem phòng',
      subtitle: room != null
          ? '${room.roomCode} • ${property.propertyName}'
          : property.propertyName,
      child: ViewingRequestBottomSheet(property: property, room: room),
    );
  }

  @override
  State<ViewingRequestBottomSheet> createState() =>
      _ViewingRequestBottomSheetState();
}

class _ViewingRequestBottomSheetState extends State<ViewingRequestBottomSheet> {
  DateTime _selectedDate = DateTime.now().add(const Duration(days: 1));
  String _selectedSlot = '15:30';
  final _noteController = TextEditingController();
  final _phoneController = TextEditingController(text: '0901234567');
  bool _isSubmitting = false;

  final List<String> _timeSlots = [
    '09:00',
    '10:30',
    '14:00',
    '15:30',
    '17:00',
    '18:30',
  ];

  @override
  void dispose() {
    _noteController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDate,
      firstDate: DateTime.now(),
      lastDate: DateTime.now().add(const Duration(days: 14)),
      builder: (context, child) {
        return Theme(
          data: Theme.of(context).copyWith(
            colorScheme: const ColorScheme.light(
              primary: AppColors.primary,
              onPrimary: Colors.white,
            ),
          ),
          child: child!,
        );
      },
    );
    if (picked != null) {
      setState(() => _selectedDate = picked);
    }
  }

  void _submit() async {
    setState(() => _isSubmitting = true);
    await Future.delayed(const Duration(milliseconds: 600));
    if (!mounted) return;
    setState(() => _isSubmitting = false);
    Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Date Selection
        Text('1. Chọn ngày xem phòng', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        InkWell(
          onTap: _pickDate,
          borderRadius: BorderRadius.circular(12),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            decoration: BoxDecoration(
              border: Border.all(color: AppColors.border),
              borderRadius: BorderRadius.circular(12),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    const Icon(Icons.calendar_today_rounded,
                        color: AppColors.primary, size: 20),
                    const SizedBox(width: 12),
                    Text(
                      Formatters.formatDate(_selectedDate),
                      style: AppTypography.headingSmall,
                    ),
                  ],
                ),
                Text('Đổi ngày',
                    style: AppTypography.labelSmall
                        .copyWith(color: AppColors.primary)),
              ],
            ),
          ),
        ),
        const SizedBox(height: 20),

        // Time Slot Selection
        Text('2. Chọn khung giờ tiện lợi', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        Wrap(
          spacing: 10,
          runSpacing: 10,
          children: _timeSlots.map((slot) {
            final isSelected = _selectedSlot == slot;
            return ChoiceChip(
              label: Text(slot),
              selected: isSelected,
              selectedColor: AppColors.primary,
              backgroundColor: AppColors.surfaceVariant,
              labelStyle: TextStyle(
                color: isSelected ? Colors.white : AppColors.textPrimary,
                fontWeight: isSelected ? FontWeight.bold : FontWeight.w500,
                fontSize: 13,
              ),
              side: BorderSide(
                color: isSelected ? AppColors.primary : AppColors.border,
              ),
              onSelected: (val) {
                if (val) setState(() => _selectedSlot = slot);
              },
            );
          }).toList(),
        ),
        const SizedBox(height: 20),

        // Contact phone
        AppTextField(
          label: 'Số điện thoại liên hệ xác nhận',
          controller: _phoneController,
          keyboardType: TextInputType.phone,
          prefixIcon: const Icon(Icons.phone_outlined),
        ),
        const SizedBox(height: 14),

        // Note
        AppTextField(
          label: 'Ghi chú cho Quản lý (tùy chọn)',
          hint: 'vd: Em muốn xem thêm chỗ phơi đồ và bãi gửi xe...',
          controller: _noteController,
          maxLines: 2,
        ),
        const SizedBox(height: 24),

        AppButton(
          text: 'Gửi yêu cầu đặt lịch hẹn',
          isLoading: _isSubmitting,
          icon: const Icon(Icons.send_rounded, color: Colors.white, size: 18),
          onPressed: _submit,
        ),
        const SizedBox(height: 10),
      ],
    );
  }
}
