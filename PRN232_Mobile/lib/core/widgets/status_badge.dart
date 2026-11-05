import 'package:flutter/material.dart';
import '../constants/app_colors.dart';
import '../constants/app_typography.dart';

enum StatusTone {
  teal,
  green,
  amber,
  rose,
  slate,
}

class StatusBadge extends StatelessWidget {
  final String label;
  final StatusTone? tone;

  const StatusBadge({
    super.key,
    required this.label,
    this.tone,
  });

  /// Tự động suy luận màu dựa trên trạng thái chuẩn của hệ thống PRN232
  static StatusTone inferTone(String status) {
    switch (status.trim().toLowerCase()) {
      case 'active':
      case 'completed':
      case 'approved':
      case 'success':
      case 'paid':
      case 'available':
      case 'đã duyệt':
      case 'còn phòng':
      case 'đã thanh toán':
      case 'hoàn thành':
        return StatusTone.green;

      case 'deposited':
      case 'reserved':
      case 'đã cọc':
      case 'giữ chỗ':
        return StatusTone.teal;

      case 'pending':
      case 'inprogress':
      case 'in_progress':
      case 'new':
      case 'unpaid':
      case 'chờ duyệt':
      case 'đang xử lý':
      case 'chưa thanh toán':
      case 'mới':
        return StatusTone.amber;

      case 'cancelled':
      case 'rejected':
      case 'overdue':
      case 'maintenance':
      case 'đã hủy':
      case 'từ chối':
      case 'quá hạn':
      case 'bảo trì':
        return StatusTone.rose;

      case 'occupied':
      case 'đang thuê':
      default:
        return StatusTone.slate;
    }
  }

  @override
  Widget build(BuildContext context) {
    final effectiveTone = tone ?? inferTone(label);

    Color bg;
    Color textColor;

    switch (effectiveTone) {
      case StatusTone.teal:
        bg = AppColors.primaryLight;
        textColor = AppColors.primary;
        break;
      case StatusTone.green:
        bg = AppColors.successBg;
        textColor = AppColors.successText;
        break;
      case StatusTone.amber:
        bg = AppColors.warningBg;
        textColor = AppColors.warningText;
        break;
      case StatusTone.rose:
        bg = AppColors.dangerBg;
        textColor = AppColors.dangerText;
        break;
      case StatusTone.slate:
        bg = AppColors.neutralBg;
        textColor = AppColors.neutralText;
        break;
    }

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: AppTypography.labelSmall.copyWith(
          color: textColor,
          fontSize: 11,
          fontWeight: FontWeight.w700,
        ),
      ),
    );
  }
}
