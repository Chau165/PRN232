import 'package:dio/dio.dart';
import 'package:intl/intl.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class InvoiceService {
  final Dio _dio = DioClient.instance;
  final DateFormat _monthFormat = DateFormat('yyyy-MM-01');
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');

  /// Lấy danh sách hoá đơn của khách thuê
  Future<List<InvoiceModel>> getCustomerInvoices({int? contractId, String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (contractId != null) queryParams['contractId'] = contractId;
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.invoices,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((i) => InvoiceModel.fromJson(i as Map<String, dynamic>)).toList();
        }
      }
      return CustomerMockData.invoices;
    } catch (_) {
      return CustomerMockData.invoices;
    }
  }

  /// Lấy danh sách hoá đơn cho Manager
  Future<List<ManagerInvoiceModel>> getManagerInvoices({int? contractId, String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (contractId != null) queryParams['contractId'] = contractId;
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.invoices,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((i) => ManagerInvoiceModel.fromJson(i as Map<String, dynamic>)).toList();
        }
      }
      return ManagerMockData.invoices;
    } catch (_) {
      return ManagerMockData.invoices;
    }
  }

  /// Lấy chi tiết một hoá đơn (bóc tách từng dòng tiền phòng, điện, nước, dịch vụ)
  Future<InvoiceModel> getInvoiceDetail(int id) async {
    try {
      final response = await _dio.get(ApiEndpoints.invoiceDetail(id));
      if (response.data is Map<String, dynamic>) {
        return InvoiceModel.fromJson(response.data as Map<String, dynamic>);
      }
      return CustomerMockData.invoices.firstWhere((inv) => inv.invoiceId == id);
    } catch (_) {
      return CustomerMockData.invoices.firstWhere((inv) => inv.invoiceId == id);
    }
  }

  /// Khách tự thanh toán hoá đơn qua cổng trực tuyến
  Future<InvoiceModel> payInvoice(int id, String paymentMethod) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.payInvoice(id),
        data: {'paymentMethod': paymentMethod},
      );
      final data = response.data as Map<String, dynamic>;
      final inv = InvoiceModel.fromJson(data);

      final idx = CustomerMockData.invoices.indexWhere((i) => i.invoiceId == id);
      if (idx != -1) {
        CustomerMockData.invoices[idx] = inv;
      }
      return inv;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Thanh toán hoá đơn thất bại.');
    } catch (_) {
      final idx = CustomerMockData.invoices.indexWhere((i) => i.invoiceId == id);
      if (idx != -1) {
        final old = CustomerMockData.invoices[idx];
        CustomerMockData.invoices[idx] = InvoiceModel(
          invoiceId: old.invoiceId,
          invoiceCode: old.invoiceCode,
          monthYear: old.monthYear,
          dueDate: old.dueDate,
          totalAmount: old.totalAmount,
          status: 'Paid',
          paidDate: DateTime.now(),
          paymentMethod: paymentMethod,
          details: old.details,
        );
        return CustomerMockData.invoices[idx];
      }
      throw Exception('Không tìm thấy hoá đơn.');
    }
  }

  /// Quản lý ghi nhận khách trả tiền mặt trực tiếp
  Future<void> recordCash(int id) async {
    try {
      await _dio.post(ApiEndpoints.recordCashInvoice(id));
      final idx = ManagerMockData.invoices.indexWhere((i) => i.invoiceId == id);
      if (idx != -1) {
        ManagerMockData.invoices[idx].status = 'Paid';
        ManagerMockData.invoices[idx].paymentMethod = 'Tiền mặt';
      }
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Ghi nhận tiền mặt thất bại.');
    } catch (_) {
      final idx = ManagerMockData.invoices.indexWhere((i) => i.invoiceId == id);
      if (idx != -1) {
        ManagerMockData.invoices[idx].status = 'Paid';
        ManagerMockData.invoices[idx].paymentMethod = 'Tiền mặt';
      }
    }
  }

  /// Quản lý xuất hoá đơn tháng mới cho hợp đồng
  Future<ManagerInvoiceModel> createInvoice({
    required int contractId,
    required DateTime invoiceMonth,
    required DateTime dueDate,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.invoices,
        data: {
          'contractId': contractId,
          'invoiceMonth': _monthFormat.format(invoiceMonth),
          'dueDate': _dateFormat.format(dueDate),
        },
      );

      final data = response.data as Map<String, dynamic>;
      final invoice = ManagerInvoiceModel.fromJson(data);
      ManagerMockData.invoices.insert(0, invoice);
      return invoice;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Xuất hoá đơn không thành công.');
    } catch (e) {
      throw Exception('Lỗi kết nối máy chủ: $e');
    }
  }

  /// Gửi khiếu nại hoá đơn điện nước
  Future<void> createComplaint({
    required int invoiceId,
    required String feeType,
    required String content,
  }) async {
    try {
      await _dio.post(
        ApiEndpoints.invoiceComplaints,
        data: {
          'invoiceId': invoiceId,
          'feeType': feeType,
          'content': content.trim(),
        },
      );
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Gửi khiếu nại thất bại.');
    } catch (_) {
      // Thành công local
    }
  }
}
