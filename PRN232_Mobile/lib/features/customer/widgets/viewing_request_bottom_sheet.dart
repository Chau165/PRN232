import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/viewing_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_text_field.dart';
import '../models/customer_models.dart';

class ViewingRequestBottomSheet extends StatefulWidget {
  final PropertyItem property;
  final RoomItem? room;
  final List<RoomItem> availableRooms;

  const ViewingRequestBottomSheet({
    super.key,
    required this.property,
    this.room,
    this.availableRooms = const [],
  });

  static Future<bool?> show(
    BuildContext context, {
    required PropertyItem property,
    RoomItem? room,
    List<RoomItem> availableRooms = const [],
  }) {
    return AppBottomSheet.show<bool>(
      context: context,
      title: 'Đặt lịch xem phòng',
      subtitle: room != null
          ? '${room.typeName} (${room.roomCode}) • ${property.propertyName}'
          : property.propertyName,
      child: ViewingRequestBottomSheet(
        property: property,
        room: room,
        availableRooms: availableRooms,
      ),
    );
  }

  @override
  State<ViewingRequestBottomSheet> createState() =>
      _ViewingRequestBottomSheetState();
}

class _ViewingRequestBottomSheetState extends State<ViewingRequestBottomSheet> {
  late DateTime _selectedDate;
  TimeOfDay _selectedTime = const TimeOfDay(hour: 9, minute: 0);
  int? _selectedRoomId;
  final _noteController = TextEditingController();
  final _phoneController = TextEditingController(text: '0901234567');
  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    // Khởi tạo ngày hẹn: Mặc định ngày mai lúc 09:00 để luôn ở tương lai
    _selectedDate = DateTime.now().add(const Duration(days: 1));

    final candidateRooms = widget.availableRooms.isNotEmpty
        ? widget.availableRooms
        : widget.property.rooms;

    if (widget.room != null) {
      _selectedRoomId = widget.room!.roomId;
    } else if (candidateRooms.isNotEmpty) {
      _selectedRoomId = candidateRooms.first.roomId;
    }
  }

  @override
  void dispose() {
    _noteController.dispose();
    _phoneController.dispose();
    super.dispose();
  }

  Future<void> _pickDate() async {
    final now = DateTime.now();
    final picked = await showDatePicker(
      context: context,
      initialDate: _selectedDate.isBefore(now) ? now : _selectedDate,
      firstDate: now,
      lastDate: now.add(const Duration(days: 30)),
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

  Future<void> _pickTime() async {
    final picked = await showTimePicker(
      context: context,
      initialTime: _selectedTime,
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
      setState(() => _selectedTime = picked);
    }
  }

  void _submit() async {
    final targetRoomId = _selectedRoomId ?? widget.room?.roomId ?? 0;

    if (targetRoomId <= 0) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: AppColors.error,
          content: Text('Vui lòng chọn phòng cần xem trước khi gửi yêu cầu.'),
        ),
      );
      return;
    }

    // Ghép ngày và giờ được chọn
    final scheduledAt = DateTime(
      _selectedDate.year,
      _selectedDate.month,
      _selectedDate.day,
      _selectedTime.hour,
      _selectedTime.minute,
    );

    // Kiểm tra thời gian hẹn phải ở tương lai
    if (scheduledAt.isBefore(DateTime.now())) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: AppColors.error,
          content: Text('Thời gian hẹn xem phòng phải ở thời điểm tương lai.'),
        ),
      );
      return;
    }

    setState(() => _isSubmitting = true);

    try {
      await ViewingService().createViewing(
        roomId: targetRoomId,
        scheduledAt: scheduledAt,
        note: _noteController.text.trim().isNotEmpty
            ? _noteController.text.trim()
            : null,
      );
      if (!mounted) return;
      setState(() => _isSubmitting = false);
      Navigator.pop(context, true);
    } catch (e) {
      if (!mounted) return;
      setState(() => _isSubmitting = false);
      final errMsg = e.toString().replaceFirst('Exception: ', '');
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          backgroundColor: AppColors.error,
          content: Text(errMsg),
          duration: const Duration(seconds: 4),
        ),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    final rawRooms = widget.availableRooms.isNotEmpty
        ? widget.availableRooms
        : widget.property.rooms;
    final seen = <int>{};
    final candidateRooms = rawRooms.where((r) => seen.add(r.roomId)).toList();
    final effectiveSelectedId = (candidateRooms.any((r) => r.roomId == _selectedRoomId))
        ? _selectedRoomId
        : (candidateRooms.isNotEmpty ? candidateRooms.first.roomId : null);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // 1. Thông tin / Chọn phòng muốn xem
        if (widget.room != null) ...[
          Text('1. Phòng muốn xem', style: AppTypography.labelLarge),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
            decoration: BoxDecoration(
              border: Border.all(color: AppColors.primaryLight),
              borderRadius: BorderRadius.circular(12),
              color: AppColors.primaryLight.withOpacity(0.3),
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Expanded(
                  child: Row(
                    children: [
                      const Icon(Icons.meeting_room_rounded, color: AppColors.primary, size: 20),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          '${widget.room!.typeName} (${widget.room!.roomCode})',
                          style: AppTypography.labelMedium.copyWith(fontWeight: FontWeight.bold),
                          overflow: TextOverflow.ellipsis,
                        ),
                      ),
                    ],
                  ),
                ),
                Text(
                  Formatters.formatCurrency(widget.room!.price),
                  style: AppTypography.labelSmall.copyWith(color: AppColors.primary, fontWeight: FontWeight.bold),
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
        ] else if (candidateRooms.isNotEmpty) ...[
          Text('1. Chọn phòng muốn xem', style: AppTypography.labelLarge),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 4),
            decoration: BoxDecoration(
              border: Border.all(color: AppColors.border),
              borderRadius: BorderRadius.circular(12),
              color: Colors.white,
            ),
            child: DropdownButtonHideUnderline(
              child: DropdownButton<int>(
                value: effectiveSelectedId,
                isExpanded: true,
                icon: const Icon(Icons.arrow_drop_down_rounded, color: AppColors.primary),
                items: candidateRooms.map((room) {
                  return DropdownMenuItem<int>(
                    value: room.roomId,
                    child: Row(
                      mainAxisAlignment: MainAxisAlignment.spaceBetween,
                      children: [
                        Expanded(
                          child: Text(
                            '${room.typeName} (${room.roomCode})',
                            style: AppTypography.labelMedium.copyWith(fontWeight: FontWeight.w600),
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                        Text(
                          Formatters.formatCurrency(room.price),
                          style: AppTypography.labelSmall.copyWith(color: AppColors.primary, fontWeight: FontWeight.bold),
                        ),
                      ],
                    ),
                  );
                }).toList(),
                onChanged: (id) {
                  if (id != null) setState(() => _selectedRoomId = id);
                },
              ),
            ),
          ),
          const SizedBox(height: 16),
        ],

        // 2. Chọn ngày xem phòng
        Text('2. Chọn ngày xem phòng', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        InkWell(
          onTap: _pickDate,
          borderRadius: BorderRadius.circular(12),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            decoration: BoxDecoration(
              border: Border.all(color: AppColors.border),
              borderRadius: BorderRadius.circular(12),
              color: Colors.white,
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
                        .copyWith(color: AppColors.primary, fontWeight: FontWeight.bold)),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),

        // 3. Chọn giờ xem phòng (Cho phép chọn giờ bất kỳ)
        Text('3. Chọn giờ xem phòng', style: AppTypography.labelLarge),
        const SizedBox(height: 8),
        InkWell(
          onTap: _pickTime,
          borderRadius: BorderRadius.circular(12),
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            decoration: BoxDecoration(
              border: Border.all(color: AppColors.border),
              borderRadius: BorderRadius.circular(12),
              color: Colors.white,
            ),
            child: Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Row(
                  children: [
                    const Icon(Icons.access_time_rounded,
                        color: AppColors.primary, size: 20),
                    const SizedBox(width: 12),
                    Text(
                      '${_selectedTime.hour.toString().padLeft(2, '0')}:${_selectedTime.minute.toString().padLeft(2, '0')}',
                      style: AppTypography.headingSmall,
                    ),
                  ],
                ),
                Text('Đổi giờ',
                    style: AppTypography.labelSmall
                        .copyWith(color: AppColors.primary, fontWeight: FontWeight.bold)),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),

        // 4. Số điện thoại liên hệ
        AppTextField(
          label: 'Số điện thoại liên hệ',
          controller: _phoneController,
          keyboardType: TextInputType.phone,
          prefixIcon: const Icon(Icons.phone_rounded, size: 18),
        ),
        const SizedBox(height: 12),

        // 5. Ghi chú
        AppTextField(
          label: 'Ghi chú cho Quản lý (nếu có)',
          controller: _noteController,
          hint: 'Ví dụ: Tôi muốn xem thêm chỗ để xe máy...',
          maxLines: 2,
        ),
        const SizedBox(height: 24),

        // Submit Button
        AppButton(
          text: 'Gửi yêu cầu hẹn xem',
          isLoading: _isSubmitting,
          onPressed: _submit,
        ),
        const SizedBox(height: 12),
      ],
    );
  }
}
