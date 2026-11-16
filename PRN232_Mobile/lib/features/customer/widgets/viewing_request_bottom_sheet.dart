import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/viewing_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
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
      subtitle: 'Chọn giờ bạn muốn đến xem, hoặc để Admin phân công Manager và xác nhận lịch.',
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
  final ViewingService _viewingService = ViewingService();

  // true: Tự chọn ngày giờ, false: Để Admin phân công lịch
  bool _selfPickTime = true;
  late DateTime _selectedDate;
  TimeOfDay _selectedTime = const TimeOfDay(hour: 9, minute: 0);
  String? _validationError;
  bool _isSubmitting = false;
  bool _isSuccess = false;

  @override
  void initState() {
    super.initState();
    // Khởi tạo ngày mai lúc 09:00 để luôn là thời điểm trong tương lai
    _selectedDate = DateTime.now().add(const Duration(days: 1));
  }

  DateTime get _combinedDateTime => DateTime(
        _selectedDate.year,
        _selectedDate.month,
        _selectedDate.day,
        _selectedTime.hour,
        _selectedTime.minute,
      );

  Future<void> _pickDateTime() async {
    final now = DateTime.now();
    final pickedDate = await showDatePicker(
      context: context,
      initialDate: _selectedDate.isBefore(now) ? now : _selectedDate,
      firstDate: now,
      lastDate: now.add(const Duration(days: 30)),
      builder: (context, child) => Theme(
        data: Theme.of(context).copyWith(
          colorScheme: const ColorScheme.light(
            primary: Color(0xFF087775),
            onPrimary: Colors.white,
          ),
        ),
        child: child!,
      ),
    );

    if (pickedDate == null || !mounted) return;

    final pickedTime = await showTimePicker(
      context: context,
      initialTime: _selectedTime,
      builder: (context, child) => Theme(
        data: Theme.of(context).copyWith(
          colorScheme: const ColorScheme.light(
            primary: Color(0xFF087775),
            onPrimary: Colors.white,
          ),
        ),
        child: child!,
      ),
    );

    if (pickedTime == null || !mounted) return;

    setState(() {
      _selectedDate = pickedDate;
      _selectedTime = pickedTime;
      _validationError = null;
    });
  }

  void _submit() async {
    if (_selfPickTime) {
      if (_combinedDateTime.isBefore(DateTime.now())) {
        setState(() => _validationError = 'Vui lòng chọn thời điểm xem trong tương lai.');
        return;
      }
    }

    setState(() {
      _isSubmitting = true;
      _validationError = null;
    });

    try {
      await _viewingService.createViewing(
        roomId: widget.room?.roomId,
        propertyId: widget.property.propertyId,
        scheduledAt: _selfPickTime ? _combinedDateTime : null,
        note: _selfPickTime ? null : 'Để Admin phân công lịch',
      );

      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _isSuccess = true;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isSubmitting = false;
        _validationError = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    final room = widget.room;
    final prop = widget.property;
    final roomTypeName = room?.typeName ?? 'Phòng tiêu chuẩn';
    final rentPrice = room?.price ?? prop.startingPrice;
    final availableCount = prop.availableRoomCount > 0 ? prop.availableRoomCount : 3;

    if (_isSuccess) {
      final timeStr = _selfPickTime
          ? '${Formatters.formatTime(_combinedDateTime)} ${Formatters.formatDate(_combinedDateTime)}'
          : 'Admin phân công lịch';

      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 20),
        child: Column(
          children: [
            const Icon(Icons.check_circle_rounded, color: Color(0xFF087775), size: 54),
            const SizedBox(height: 12),
            const Text(
              'Gửi yêu cầu thành công!',
              style: TextStyle(
                fontSize: 18,
                fontWeight: FontWeight.bold,
                color: Color(0xFF0F172A),
              ),
            ),
            const SizedBox(height: 8),
            Text(
              'Đã gửi yêu cầu xem $roomTypeName tại khu trọ ($timeStr). Chưa có phòng cụ thể được giữ; Admin sẽ phân công Manager.',
              textAlign: TextAlign.center,
              style: const TextStyle(fontSize: 13, color: Color(0xFF64748B), height: 1.4),
            ),
            const SizedBox(height: 24),
            AppButton(
              text: 'Đã hiểu',
              onPressed: () => Navigator.of(context).pop(true),
            ),
          ],
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Grid thông tin tóm tắt 4 ô (Hình 3)
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFF8FAFC),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFE2E8F0)),
          ),
          child: Column(
            children: [
              Row(
                children: [
                  Expanded(
                    child: _buildInfoItem('Khu trọ', prop.propertyName),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _buildInfoItem('Loại phòng', roomTypeName),
                  ),
                ],
              ),
              const Divider(height: 20),
              Row(
                children: [
                  Expanded(
                    child: _buildInfoItem(
                      'Giá tham khảo hiện tại',
                      '${Formatters.formatCurrency(rentPrice)}/tháng',
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: _buildInfoItem(
                      'Khả dụng tại thời điểm kiểm tra',
                      '$availableCount phòng',
                    ),
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),

        // Section: Thời gian xem phòng
        const Text(
          'Thời gian xem phòng',
          style: TextStyle(
            fontSize: 15,
            fontWeight: FontWeight.w700,
            color: Color(0xFF0F172A),
          ),
        ),
        const SizedBox(height: 10),

        // Radio 1: Tự chọn ngày giờ
        InkWell(
          onTap: () => setState(() => _selfPickTime = true),
          borderRadius: BorderRadius.circular(8),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              children: [
                Radio<bool>(
                  value: true,
                  groupValue: _selfPickTime,
                  activeColor: const Color(0xFF087775),
                  onChanged: (val) => setState(() => _selfPickTime = val ?? true),
                ),
                const Text(
                  'Tự chọn ngày giờ',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                    color: Color(0xFF334155),
                  ),
                ),
              ],
            ),
          ),
        ),

        if (_selfPickTime) ...[
          Padding(
            padding: const EdgeInsets.only(left: 48, right: 8, bottom: 8),
            child: InkWell(
              onTap: _pickDateTime,
              borderRadius: BorderRadius.circular(8),
              child: Container(
                padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
                decoration: BoxDecoration(
                  color: Colors.white,
                  borderRadius: BorderRadius.circular(8),
                  border: Border.all(
                    color: _validationError != null
                        ? const Color(0xFFE11D48)
                        : const Color(0xFFCBD5E1),
                  ),
                ),
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      '${Formatters.formatDate(_selectedDate)} ${Formatters.formatTime(_combinedDateTime)}',
                      style: const TextStyle(fontSize: 14, color: Color(0xFF0F172A)),
                    ),
                    const Icon(Icons.calendar_month_outlined,
                        size: 20, color: Color(0xFF64748B)),
                  ],
                ),
              ),
            ),
          ),
        ],

        // Radio 2: Để Admin phân công lịch
        InkWell(
          onTap: () => setState(() => _selfPickTime = false),
          borderRadius: BorderRadius.circular(8),
          child: Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Row(
              children: [
                Radio<bool>(
                  value: false,
                  groupValue: _selfPickTime,
                  activeColor: const Color(0xFF087775),
                  onChanged: (val) => setState(() => _selfPickTime = val ?? false),
                ),
                const Text(
                  'Để Admin phân công lịch',
                  style: TextStyle(
                    fontSize: 14,
                    fontWeight: FontWeight.w500,
                    color: Color(0xFF334155),
                  ),
                ),
              ],
            ),
          ),
        ),

        if (_validationError != null) ...[
          const SizedBox(height: 6),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 12),
            child: Text(
              _validationError!,
              style: const TextStyle(fontSize: 12, color: Color(0xFFE11D48)),
            ),
          ),
        ],
        const SizedBox(height: 24),

        // Submit Button
        ElevatedButton(
          onPressed: _isSubmitting ? null : _submit,
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF087775),
            foregroundColor: Colors.white,
            padding: const EdgeInsets.symmetric(vertical: 14),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            elevation: 0,
          ),
          child: _isSubmitting
              ? const SizedBox(
                  height: 20,
                  width: 20,
                  child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                )
              : const Text(
                  'Gửi yêu cầu xem loại phòng',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
        ),
      ],
    );
  }

  Widget _buildInfoItem(String title, String value) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: const TextStyle(fontSize: 12, color: Color(0xFF94A3B8)),
        ),
        const SizedBox(height: 4),
        Text(
          value,
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: Color(0xFF334155),
          ),
        ),
      ],
    );
  }
}
