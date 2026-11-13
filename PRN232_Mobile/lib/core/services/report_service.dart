import 'package:dio/dio.dart';
import 'package:intl/intl.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class RevenueReportModel {
  final num totalCollected;
  final int paymentCount;
  final num unknownDateAmount;
  final int unknownDatePaymentCount;

  const RevenueReportModel({
    required this.totalCollected,
    required this.paymentCount,
    required this.unknownDateAmount,
    required this.unknownDatePaymentCount,
  });

  factory RevenueReportModel.fromJson(Map<String, dynamic> json) {
    return RevenueReportModel(
      totalCollected: json['totalCollected'] ?? 0,
      paymentCount: json['paymentCount'] ?? 0,
      unknownDateAmount: json['unknownDateAmount'] ?? 0,
      unknownDatePaymentCount: json['unknownDatePaymentCount'] ?? 0,
    );
  }
}

class ReceivablesReportModel {
  final num outstandingAmount;
  final int outstandingInvoiceCount;
  final num overdueAmount;
  final int overdueInvoiceCount;

  const ReceivablesReportModel({
    required this.outstandingAmount,
    required this.outstandingInvoiceCount,
    required this.overdueAmount,
    required this.overdueInvoiceCount,
  });

  factory ReceivablesReportModel.fromJson(Map<String, dynamic> json) {
    return ReceivablesReportModel(
      outstandingAmount: json['outstandingAmount'] ?? 0,
      outstandingInvoiceCount: json['outstandingInvoiceCount'] ?? 0,
      overdueAmount: json['overdueAmount'] ?? 0,
      overdueInvoiceCount: json['overdueInvoiceCount'] ?? 0,
    );
  }
}

class ReportService {
  final Dio _dio = DioClient.instance;
  final DateFormat _monthFormat = DateFormat('yyyy-MM-01');
  final DateFormat _dayFormat = DateFormat('yyyy-MM-dd');

  Future<RevenueReportModel?> getRevenueReport({int? propertyId, DateTime? month}) async {
    try {
      final targetMonth = month ?? DateTime.now();
      final monthStr = _monthFormat.format(targetMonth);

      final queryParams = <String, dynamic>{
        'fromMonth': monthStr,
        'toMonth': monthStr,
      };
      if (propertyId != null) queryParams['propertyId'] = propertyId;

      final response = await _dio.get(
        ApiEndpoints.reportsRevenue,
        queryParameters: queryParams,
      );

      if (response.data is Map<String, dynamic>) {
        return RevenueReportModel.fromJson(response.data as Map<String, dynamic>);
      }
      return null;
    } catch (_) {
      return null;
    }
  }

  Future<ReceivablesReportModel?> getReceivablesReport({int? propertyId}) async {
    try {
      final queryParams = <String, dynamic>{
        'asOfDate': _dayFormat.format(DateTime.now()),
      };
      if (propertyId != null) queryParams['propertyId'] = propertyId;

      final response = await _dio.get(
        ApiEndpoints.reportsReceivables,
        queryParameters: queryParams,
      );

      if (response.data is Map<String, dynamic>) {
        return ReceivablesReportModel.fromJson(response.data as Map<String, dynamic>);
      }
      return null;
    } catch (_) {
      return null;
    }
  }
}
