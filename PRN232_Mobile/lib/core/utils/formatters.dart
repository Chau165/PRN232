import 'package:intl/intl.dart';

class Formatters {
  Formatters._();

  static final NumberFormat _currencyFormat = NumberFormat.currency(
    locale: 'vi_VN',
    symbol: '₫',
    decimalDigits: 0,
  );

  static final DateFormat _dateFormat = DateFormat('dd/MM/yyyy');
  static final DateFormat _dateTimeFormat = DateFormat('HH:mm - dd/MM/yyyy');
  static final DateFormat _monthYearFormat = DateFormat('MM/yyyy');

  /// Định dạng tiền tệ đầy đủ: 2.500.000 ₫
  static String formatCurrency(num? amount) {
    if (amount == null) return '0 ₫';
    return _currencyFormat.format(amount).trim();
  }

  /// Định dạng rút gọn cho card: 2.5 tr/tháng hoặc 350k/người
  static String formatShortPrice(num? amount) {
    if (amount == null) return '0 ₫';
    if (amount >= 1000000) {
      final millions = amount / 1000000;
      final formatted = millions % 1 == 0
          ? millions.toInt().toString()
          : millions.toStringAsFixed(1);
      return '$formatted tr/tháng';
    }
    if (amount >= 1000) {
      return '${(amount / 1000).toInt()}k';
    }
    return formatCurrency(amount);
  }

  /// Định dạng ngày: dd/MM/yyyy
  static String formatDate(DateTime? date) {
    if (date == null) return '--';
    return _dateFormat.format(date);
  }

  /// Parse ISO string sang dd/MM/yyyy
  static String formatDateString(String? isoString) {
    if (isoString == null || isoString.isEmpty) return '--';
    try {
      final dt = DateTime.parse(isoString);
      return _dateFormat.format(dt.toLocal());
    } catch (_) {
      return isoString;
    }
  }

  /// Định dạng tháng/năm: MM/yyyy
  static String formatMonthYear(DateTime? date) {
    if (date == null) return '--';
    return _monthYearFormat.format(date);
  }

  /// Định dạng ngày giờ: HH:mm - dd/MM/yyyy
  static String formatDateTime(DateTime? dateTime) {
    if (dateTime == null) return '--';
    return _dateTimeFormat.format(dateTime);
  }

  static String formatDateTimeString(String? isoString) {
    if (isoString == null || isoString.isEmpty) return '--';
    try {
      final dt = DateTime.parse(isoString);
      return _dateTimeFormat.format(dt.toLocal());
    } catch (_) {
      return isoString;
    }
  }

  /// Định dạng diện tích: 25 m²
  static String formatArea(num? area) {
    if (area == null) return '-- m²';
    return '$area m²';
  }
}
