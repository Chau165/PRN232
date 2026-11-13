import 'package:flutter/material.dart';

/// Bảng màu chuẩn của hệ thống TrọViệt (PRN232)
/// Đồng bộ 100% với Tailwind CSS & index.css từ PRN232_FE
class AppColors {
  AppColors._();

  // Brand Colors (TrọViệt Teal)
  static const Color primary = Color(0xFF087775);
  static const Color primaryLight = Color(0xFFE8F5F2);
  static const Color primaryHover = Color(0xFF065E5C);
  static const Color primaryDark = Color(0xFF044241);

  // Surface & Background
  static const Color background = Color(0xFFFCFDFD);
  static const Color surface = Colors.white;
  static const Color surfaceVariant = Color(0xFFF5F7F7);
  static const Color divider = Color(0xFFE2E8F0);

  // Slate Neutral Colors (Typography & Borders)
  static const Color textPrimary = Color(0xFF0F172A); // slate-900
  static const Color textSecondary = Color(0xFF475569); // slate-600
  static const Color textMuted = Color(0xFF64748B); // slate-500
  static const Color textLight = Color(0xFF94A3B8); // slate-400
  static const Color border = Color(0xFFE2E8F0); // slate-200
  static const Color borderLight = Color(0xFFF1F5F9); // slate-100

  // Status Colors (Đồng bộ với StatusBadge web: teal, green, amber, rose, slate)
  // Success / Active / Approved
  static const Color success = Color(0xFF059669); // emerald-600
  static const Color successBg = Color(0xFFECFDF5); // emerald-50
  static const Color successText = Color(0xFF047857); // emerald-700

  // Warning / Pending / Review
  static const Color warning = Color(0xFFD97706); // amber-600
  static const Color warningBg = Color(0xFFFFFBEB); // amber-50
  static const Color warningText = Color(0xFFB45309); // amber-700

  // Danger / Cancelled / Rejected / Overdue / Error
  static const Color danger = Color(0xFFE11D48); // rose-600
  static const Color dangerBg = Color(0xFFFFF1F2); // rose-50
  static const Color dangerText = Color(0xFFBE123C); // rose-700
  static const Color error = danger;
  static const Color errorBg = dangerBg;
  static const Color errorText = dangerText;

  // Info / Blue
  static const Color info = Color(0xFF2563EB); // blue-600
  static const Color infoBg = Color(0xFFEFF6FF); // blue-50
  static const Color infoText = Color(0xFF1D4ED8); // blue-700

  // Slate / Neutral status
  static const Color neutralBg = Color(0xFFF1F5F9); // slate-100
  static const Color neutralText = Color(0xFF475569); // slate-600
}
