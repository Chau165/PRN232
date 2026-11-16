import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../routes/app_routes.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';
import '../widgets/payos_qr_dialog.dart';

class DepositScreen extends StatefulWidget {
  final PropertyItem property;
  final RoomItem room;
  final CustomerBookingModel? initialBooking;

  const DepositScreen({
    super.key,
    required this.property,
    required this.room,
    this.initialBooking,
  });

  @override
  State<DepositScreen> createState() => _DepositScreenState();
}

class _DepositScreenState extends State<DepositScreen> {
  final BookingService _bookingService = BookingService();

  // Step 1: Hold room, Step 2: Pay deposit via payOS
  int _currentStep = 1;
  CustomerBookingModel? _createdBooking;

  // Form inputs for Step 1
  late DateTime _moveInDate;
  int _occupantCount = 1;
  late final TextEditingController _occupantController;
  String? _dateError;
  String? _occupantError;
  String? _generalError;
  bool _isHolding = false;

  @override
  void initState() {
    super.initState();
    _moveInDate = DateTime.now().add(const Duration(days: 4));
    _occupantController = TextEditingController(text: _occupantCount.toString());

    if (widget.initialBooking != null) {
      _createdBooking = widget.initialBooking;
      _currentStep = 2;
    }
  }

  @override
  void dispose() {
    _occupantController.dispose();
    super.dispose();
  }

  void _validateAndHoldRoom() async {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final pickedDateOnly = DateTime(_moveInDate.year, _moveInDate.month, _moveInDate.day);

    setState(() {
      _dateError = null;
      _occupantError = null;
      _generalError = null;
    });

    bool hasError = false;

    if (pickedDateOnly.isBefore(today)) {
      setState(() => _dateError = 'Ngày dọn vào không được ở quá khứ.');
      hasError = true;
    } else if (pickedDateOnly.difference(today).inDays > 14) {
      setState(() => _dateError = 'Ngày dọn vào tối đa 14 ngày kể từ hôm nay.');
      hasError = true;
    }

    final maxOccupancy = widget.room.maxOccupancy > 0 ? widget.room.maxOccupancy : 2;
    if (_occupantCount < 1) {
      setState(() => _occupantError = 'Số người ở tối thiểu là 1.');
      hasError = true;
    } else if (_occupantCount > maxOccupancy) {
      setState(() =>
          _occupantError = 'Phòng ${widget.room.roomCode} chỉ được ở tối đa $maxOccupancy người.');
      hasError = true;
    }

    if (hasError) return;

    setState(() => _isHolding = true);

    try {
      final booking = await _bookingService.createBooking(
        widget.room.roomId,
        moveInDate: _moveInDate,
        occupantCount: _occupantCount,
      );

      if (!mounted) return;
      setState(() {
        _isHolding = false;
        _createdBooking = booking;
        _currentStep = 2;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isHolding = false;
        _generalError = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  void _openPayOS() async {
    final booking = _createdBooking;
    if (booking == null) return;

    // Hiển thị loading nhẹ khi gọi tạo payment link
    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(
        child: CircularProgressIndicator(color: Color(0xFF087775)),
      ),
    );

    BookingPaymentLinkModel? link;
    try {
      link = await _bookingService.createPaymentLink(booking.id);
    } catch (_) {}

    if (!mounted) return;
    Navigator.of(context).pop(); // Đóng loading

    // Mở màn hình QR Code payOS ngay trên App
    PayOSQRCodeDialog.show(
      context,
      booking: booking,
      paymentLink: link,
      onPaymentSuccess: () {
        context.pushReplacement(
          AppRoutes.paymentSuccess,
          extra: {
            'title': 'Đặt cọc thành công!',
            'subtitle':
                'Đã thanh toán tiền cọc qua payOS cho ${widget.room.roomCode} - ${widget.property.propertyName}',
            'amount': booking.depositAmount,
            'method': 'payOS (VietQR)',
            'code': booking.bookingCode,
          },
        );
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('Đặt cọc giữ phòng'),
        backgroundColor: Colors.white,
        elevation: 0,
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Sub-header & Title
            Center(
              child: Column(
                children: [
                  const Text(
                    'KHU VỰC CỦA BẠN',
                    style: TextStyle(
                      color: Color(0xFF087775),
                      fontWeight: FontWeight.w700,
                      fontSize: 12,
                      letterSpacing: 1.2,
                    ),
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'Đặt cọc giữ phòng',
                    style: TextStyle(
                      color: Color(0xFF0F172A),
                      fontWeight: FontWeight.w800,
                      fontSize: 24,
                    ),
                  ),
                  const SizedBox(height: 6),
                  const Text(
                    'Tiền cọc tương đương 1 tháng tiền thuê.',
                    style: TextStyle(
                      color: Color(0xFF64748B),
                      fontSize: 14,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Card content
            Container(
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withOpacity(0.04),
                    blurRadius: 16,
                    offset: const Offset(0, 4),
                  ),
                ],
              ),
              padding: const EdgeInsets.all(20),
              child: _currentStep == 1 ? _buildStep1HoldRoom() : _buildStep2PayOS(),
            ),
          ],
        ),
      ),
    );
  }

  /// Bước 1: Chọn ngày dọn vào & số người ở (Hình 4)
  Widget _buildStep1HoldRoom() {
    final user = AuthState.instance.currentUser;
    final userName = user?.fullName.isNotEmpty == true ? user!.fullName : 'BabyBoo';
    final userPhone = user?.phoneNumber.isNotEmpty == true ? user!.phoneNumber : '0123456789';
    final maxOccupancy = widget.room.maxOccupancy > 0 ? widget.room.maxOccupancy : 2;

    final holdEndDate = _moveInDate.add(const Duration(days: 3));
    final holdEndDateStr = Formatters.formatDate(holdEndDate);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Summary info
        _buildPropertySummaryRow('Khu trọ', widget.property.propertyName),
        const SizedBox(height: 12),
        _buildPropertySummaryRow(
            'Phòng', '${widget.room.roomCode} · ${widget.room.typeName}'),
        const SizedBox(height: 12),
        _buildPropertySummaryRow('Người đặt', '$userName · $userPhone'),
        const SizedBox(height: 16),

        // Deposit amount highlight banner
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          decoration: BoxDecoration(
            color: const Color(0xFFE6F4F2),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Text(
            'Tiền cọc cần thanh toán: ${Formatters.formatCurrency(widget.room.depositAmount)}',
            style: const TextStyle(
              color: Color(0xFF087775),
              fontWeight: FontWeight.w700,
              fontSize: 14,
            ),
          ),
        ),
        const SizedBox(height: 20),

        // Inputs: Ngày dọn vào dự kiến & Số người ở
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFF8FAFC),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFE2E8F0)),
          ),
          child: Column(
            children: [
              // Ngày dọn vào
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Ngày dọn vào dự kiến',
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      fontSize: 13,
                      color: Color(0xFF334155),
                    ),
                  ),
                  const SizedBox(height: 6),
                  InkWell(
                    onTap: () async {
                      final now = DateTime.now();
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: _moveInDate,
                        firstDate: now,
                        lastDate: now.add(const Duration(days: 14)),
                      );
                      if (picked != null) {
                        setState(() {
                          _moveInDate = picked;
                          _dateError = null;
                        });
                      }
                    },
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: _dateError != null
                              ? const Color(0xFFF43F5E)
                              : const Color(0xFFCBD5E1),
                        ),
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            Formatters.formatDate(_moveInDate),
                            style: const TextStyle(
                              fontSize: 14,
                              color: Color(0xFF0F172A),
                            ),
                          ),
                          const Icon(Icons.calendar_month_outlined,
                              size: 18, color: Color(0xFF64748B)),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Tối đa 14 ngày kể từ hôm nay',
                    style: TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                  ),
                  if (_dateError != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      _dateError!,
                      style: const TextStyle(fontSize: 12, color: Color(0xFFE11D48)),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: 14),

              // Số người ở
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Số người ở',
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      fontSize: 13,
                      color: Color(0xFF334155),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(
                        color: _occupantError != null
                            ? const Color(0xFFF43F5E)
                            : const Color(0xFFCBD5E1),
                      ),
                    ),
                    child: Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            controller: _occupantController,
                            keyboardType: TextInputType.number,
                            decoration: const InputDecoration(
                              border: InputBorder.none,
                              isDense: true,
                              contentPadding: EdgeInsets.symmetric(vertical: 10),
                            ),
                            onChanged: (val) {
                              final parsed = int.tryParse(val);
                              if (parsed != null) {
                                setState(() {
                                  _occupantCount = parsed;
                                  _occupantError = null;
                                });
                              }
                            },
                          ),
                        ),
                        IconButton(
                          icon: const Icon(Icons.remove_circle_outline, size: 20),
                          onPressed: _occupantCount > 1
                              ? () {
                                  setState(() {
                                    _occupantCount--;
                                    _occupantController.text = _occupantCount.toString();
                                    _occupantError = null;
                                  });
                                }
                              : null,
                          constraints: const BoxConstraints(),
                          padding: const EdgeInsets.all(4),
                        ),
                        IconButton(
                          icon: const Icon(Icons.add_circle_outline, size: 20),
                          onPressed: _occupantCount < maxOccupancy
                              ? () {
                                  setState(() {
                                    _occupantCount++;
                                    _occupantController.text = _occupantCount.toString();
                                    _occupantError = null;
                                  });
                                }
                              : null,
                          constraints: const BoxConstraints(),
                          padding: const EdgeInsets.all(4),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Tối đa $maxOccupancy người cho phòng ${widget.room.roomCode}',
                    style: const TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                  ),
                  if (_occupantError != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      _occupantError!,
                      style: const TextStyle(fontSize: 12, color: Color(0xFFE11D48)),
                    ),
                  ],
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // Terms and guidelines
        const Text(
          'Sau khi giữ phòng, bạn có 15 phút để thanh toán tiền cọc.',
          style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
        ),
        const SizedBox(height: 8),
        Text(
          'Tiền cọc được giữ làm tiền đảm bảo trong suốt thời gian thuê và hoàn lại khi trả phòng, sau khi trừ tiền phòng còn nợ và hư hỏng thiết bị (nếu có). Tháng đầu tiên bạn đóng đủ tiền phòng. Phòng được giữ đến hết ngày $holdEndDateStr. Nếu bạn hủy hoặc không đến nhận phòng trước hạn đó, tiền cọc không được hoàn. Nếu chủ trọ hủy, bạn được hoàn 100% (liên hệ trực tiếp quản lý để nhận lại tiền).',
          style: const TextStyle(fontSize: 13, color: Color(0xFF64748B), height: 1.4),
        ),
        const SizedBox(height: 16),

        if (_generalError != null) ...[
          Text(
            _generalError!,
            style: const TextStyle(fontSize: 13, color: Color(0xFFE11D48)),
          ),
          const SizedBox(height: 12),
        ],

        // Submit Button
        ElevatedButton(
          onPressed: _isHolding ? null : _validateAndHoldRoom,
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF087775),
            foregroundColor: Colors.white,
            padding: const EdgeInsets.symmetric(vertical: 14),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            elevation: 0,
          ),
          child: _isHolding
              ? const SizedBox(
                  height: 20,
                  width: 20,
                  child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                )
              : const Text(
                  'Giữ phòng và thanh toán cọc',
                  style: TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
        ),
      ],
    );
  }

  /// Bước 2: Nhấn để thanh toán qua payOS (Hình 5)
  Widget _buildStep2PayOS() {
    final booking = _createdBooking!;
    final expiryFormatted = booking.expiryDate != null
        ? Formatters.formatDateTime(booking.expiryDate)
        : Formatters.formatDateTime(DateTime.now().add(const Duration(minutes: 15)));

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Booking summary
        _buildPropertySummaryRow('Khu trọ', booking.propertyName),
        const SizedBox(height: 12),
        _buildPropertySummaryRow('Phòng', booking.roomCode),
        const SizedBox(height: 12),
        _buildPropertySummaryRow(
          'Dự kiến nhận phòng',
          'Dọn vào ${Formatters.formatDate(booking.moveInDate)} · ${booking.occupantCount} người',
        ),
        const SizedBox(height: 12),
        _buildPropertySummaryRow('Hạn thanh toán cọc', expiryFormatted),
        const SizedBox(height: 16),

        // Deposit amount highlight banner
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          decoration: BoxDecoration(
            color: const Color(0xFFE6F4F2),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Text(
            'Tiền cọc cần thanh toán: ${Formatters.formatCurrency(booking.depositAmount)}',
            style: const TextStyle(
              color: Color(0xFF087775),
              fontWeight: FontWeight.w700,
              fontSize: 14,
            ),
          ),
        ),
        const SizedBox(height: 16),

        const Text(
          'Tiền cọc được trừ vào tiền phòng tháng đầu tiên khi lập hợp đồng.',
          style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
        ),
        const SizedBox(height: 16),

        // payOS Box
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFF0FDF4),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFBBF7D0)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: const [
              Text(
                'Thanh toán cọc qua payOS',
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 14,
                  color: Color(0xFF166534),
                ),
              ),
              SizedBox(height: 6),
              Text(
                'Mở trang thanh toán để quét mã hoặc chuyển khoản. Booking chỉ được xác nhận sau khi hệ thống nhận kết quả thanh toán.',
                style: TextStyle(
                  fontSize: 13,
                  color: Color(0xFF166534),
                  height: 1.4,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 24),

        // Button pay via payOS
        ElevatedButton(
          onPressed: _openPayOS,
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF087775),
            foregroundColor: Colors.white,
            padding: const EdgeInsets.symmetric(vertical: 14),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            elevation: 0,
          ),
          child: Text(
            'Thanh toán ${Formatters.formatCurrency(booking.depositAmount)} qua payOS',
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
        ),
        const SizedBox(height: 10),
        TextButton(
          onPressed: () {
            context.pushReplacement(AppRoutes.customerBookings);
          },
          child: const Text(
            'Quay lại danh sách đặt cọc của tôi',
            style: TextStyle(color: Color(0xFF64748B)),
          ),
        ),
      ],
    );
  }

  Widget _buildPropertySummaryRow(String label, String value) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 140,
          child: Text(
            label,
            style: const TextStyle(
              fontSize: 13,
              color: Color(0xFF64748B),
            ),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w600,
              color: Color(0xFF0F172A),
            ),
          ),
        ),
      ],
    );
  }
}
