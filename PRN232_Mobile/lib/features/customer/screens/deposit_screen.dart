import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_constants.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../routes/app_routes.dart';
import '../models/customer_models.dart';

class DepositScreen extends StatefulWidget {
  final PropertyItem property;
  final RoomItem room;

  const DepositScreen({
    super.key,
    required this.property,
    required this.room,
  });

  @override
  State<DepositScreen> createState() => _DepositScreenState();
}

class _DepositScreenState extends State<DepositScreen> {
  final BookingService _bookingService = BookingService();
  String _selectedPaymentMethod = AppConstants.paymentVNPay;
  DateTime _moveInDate = DateTime.now().add(const Duration(days: 7));
  bool _agreedTerms = true;
  bool _isProcessing = false;

  void _processPayment() async {
    if (!_agreedTerms) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng đồng ý với điều khoản đặt cọc.')),
      );
      return;
    }

    setState(() => _isProcessing = true);

    try {
      // Bước 1: Tạo Booking trên backend
      final booking = await _bookingService.createBooking(widget.room.roomId);
      // Bước 2: Thanh toán cọc qua cổng giả lập
      final paidBooking = await _bookingService.payBooking(
        booking.id,
        _selectedPaymentMethod,
      );

      if (!mounted) return;
      setState(() => _isProcessing = false);

      // Chuyển sang màn hình thành công
      context.push(
        AppRoutes.paymentSuccess,
        extra: {
          'title': 'Đặt cọc thành công!',
          'subtitle':
              'Đã nhận tiền cọc cho ${widget.room.roomCode} - ${widget.property.propertyName}',
          'amount': paidBooking.depositAmount,
          'method': _selectedPaymentMethod,
          'code': paidBooking.bookingCode,
        },
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _isProcessing = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Xác nhận đặt cọc giữ chỗ'),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Room summary card
            AppCard(
              color: AppColors.primaryLight,
              border: const BorderSide(color: Color(0xFFC7E7E2)),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(widget.room.roomCode,
                      style: AppTypography.displayMedium
                          .copyWith(color: AppColors.primary)),
                  const SizedBox(height: 2),
                  Text(widget.room.typeName, style: AppTypography.headingSmall),
                  Text(widget.property.fullAddress,
                      style: AppTypography.bodySmall),
                  const Divider(height: 24),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Tiền thuê hàng tháng:',
                          style: AppTypography.bodyMedium),
                      Text(Formatters.formatCurrency(widget.room.price),
                          style: AppTypography.labelLarge),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Tiền đặt cọc giữ chỗ (1 tháng):',
                          style: AppTypography.headingSmall),
                      Text(
                        Formatters.formatCurrency(widget.room.depositAmount),
                        style: AppTypography.headingLarge
                            .copyWith(color: AppColors.primary),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Move-in date picker
            Text('Dự kiến ngày dọn vào', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              onTap: () async {
                final picked = await showDatePicker(
                  context: context,
                  initialDate: _moveInDate,
                  firstDate: DateTime.now(),
                  lastDate: DateTime.now().add(const Duration(days: 60)),
                );
                if (picked != null) {
                  setState(() => _moveInDate = picked);
                }
              },
              child: Row(
                children: [
                  const Icon(Icons.calendar_month_rounded,
                      color: AppColors.primary),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Ngày bắt đầu tính tiền phòng',
                            style: AppTypography.bodySmall),
                        Text(Formatters.formatDate(_moveInDate),
                            style: AppTypography.labelLarge),
                      ],
                    ),
                  ),
                  const Icon(Icons.arrow_forward_ios_rounded,
                      size: 14, color: AppColors.textMuted),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Payment method selector
            Text('Phương thức thanh toán cọc',
                style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              padding: EdgeInsets.zero,
              child: Column(
                children: [
                  _buildPaymentOption(
                    method: AppConstants.paymentVNPay,
                    label: 'Cổng VNPAY (Thẻ ATM / QR)',
                    badgeText: 'VNPAY',
                    badgeBg: const Color(0xFFE0F2FE),
                    badgeFg: const Color(0xFF0369A1),
                  ),
                  const Divider(height: 1),
                  _buildPaymentOption(
                    method: AppConstants.paymentMoMo,
                    label: 'Ví Điện Tử MoMo',
                    badgeText: 'MOMO',
                    badgeBg: const Color(0xFFFCE7F3),
                    badgeFg: const Color(0xFFBE185D),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Policy agreement
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Checkbox(
                  value: _agreedTerms,
                  activeColor: AppColors.primary,
                  onChanged: (val) {
                    setState(() => _agreedTerms = val ?? false);
                  },
                ),
                Expanded(
                  child: Padding(
                    padding: const EdgeInsets.only(top: 12),
                    child: Text(
                      'Tôi đã đọc và đồng ý với Quy chế giữ chỗ phòng & chính sách hoàn tiền cọc của Hệ thống TrọViệt.',
                      style: AppTypography.bodySmall,
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 24),

            // Action button
            AppButton(
              text:
                  'Thanh toán ngay • ${Formatters.formatCurrency(widget.room.depositAmount)}',
              isLoading: _isProcessing,
              onPressed: _processPayment,
            ),
            const SizedBox(height: 20),
          ],
        ),
      ),
    );
  }

  Widget _buildPaymentOption({
    required String method,
    required String label,
    required String badgeText,
    required Color badgeBg,
    required Color badgeFg,
  }) {
    final isSelected = _selectedPaymentMethod == method;
    return InkWell(
      onTap: () {
        setState(() => _selectedPaymentMethod = method);
      },
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
        child: Row(
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: badgeBg,
                borderRadius: BorderRadius.circular(6),
              ),
              child: Text(
                badgeText,
                style: TextStyle(
                  fontWeight: FontWeight.w900,
                  color: badgeFg,
                  fontSize: 12,
                ),
              ),
            ),
            const SizedBox(width: 12),
            Expanded(
              child: Text(label, style: AppTypography.labelLarge),
            ),
            Container(
              width: 20,
              height: 20,
              decoration: BoxDecoration(
                shape: BoxShape.circle,
                border: Border.all(
                  color: isSelected ? AppColors.primary : AppColors.border,
                  width: 2,
                ),
                color: isSelected ? AppColors.primary : Colors.transparent,
              ),
              child: isSelected
                  ? const Icon(Icons.check, size: 14, color: Colors.white)
                  : null,
            ),
          ],
        ),
      ),
    );
  }
}
