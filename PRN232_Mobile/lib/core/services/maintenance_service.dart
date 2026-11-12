import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class MaintenanceService {
  final Dio _dio = DioClient.instance;

  /// Lấy danh sách yêu cầu bảo trì cho khách thuê
  Future<List<CustomerMaintenanceModel>> getCustomerRequests({String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.maintenanceRequests,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((m) => CustomerMaintenanceModel.fromJson(m as Map<String, dynamic>)).toList();
        }
      }
      return CustomerMockData.maintenanceRequests;
    } catch (_) {
      return CustomerMockData.maintenanceRequests;
    }
  }

  /// Lấy danh sách yêu cầu bảo trì cho Manager
  Future<List<ManagerTaskModel>> getManagerRequests({String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.maintenanceRequests,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((m) => ManagerTaskModel.fromMaintenanceJson(m as Map<String, dynamic>)).toList();
        }
      }
      return ManagerMockData.tasks.where((t) => t.type == TaskType.maintenance).toList();
    } catch (_) {
      return ManagerMockData.tasks.where((t) => t.type == TaskType.maintenance).toList();
    }
  }

  /// Khách gửi báo hỏng sự cố phòng hoặc thiết bị
  Future<CustomerMaintenanceModel> createRequest({
    required int contractId,
    int? equipmentId,
    required String description,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.maintenanceRequests,
        data: {
          'contractId': contractId,
          if (equipmentId != null) 'equipmentId': equipmentId,
          'description': description.trim(),
        },
      );

      final data = response.data as Map<String, dynamic>;
      final req = CustomerMaintenanceModel.fromJson(data);
      CustomerMockData.maintenanceRequests.insert(0, req);
      return req;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Gửi yêu cầu báo hỏng thất bại.');
    } catch (_) {
      final newReq = CustomerMaintenanceModel(
        id: DateTime.now().millisecondsSinceEpoch % 10000,
        title: 'Báo hỏng phòng P.201',
        description: description,
        roomCode: 'P.201',
        propertyName: 'Nhà trọ BlueSky Central',
        priority: 'Bình thường',
        status: 'Pending',
        createdAt: DateTime.now(),
      );
      CustomerMockData.maintenanceRequests.insert(0, newReq);
      return newReq;
    }
  }

  /// Quản lý phân công thợ sửa chữa
  Future<void> assignTechnician(int requestId, {required int assignedTo, String? note}) async {
    try {
      await _dio.patch(
        ApiEndpoints.maintenanceDetail(requestId),
        data: {
          'assignedTo': assignedTo,
          'assignmentNote': note,
        },
      );
      _updateLocalTask(requestId, TaskStatus.inProgress);
    } catch (_) {
      _updateLocalTask(requestId, TaskStatus.inProgress);
    }
  }

  /// Quản lý nộp biên bản nghiệm thu hoàn thành sửa chữa kèm ảnh
  Future<void> submitReport(int requestId, {required String description}) async {
    try {
      final formData = FormData.fromMap({
        'description': description,
      });

      await _dio.post(
        ApiEndpoints.maintenanceReports(requestId),
        data: formData,
      );
      _updateLocalTask(requestId, TaskStatus.completed);
    } catch (_) {
      _updateLocalTask(requestId, TaskStatus.completed);
    }
  }

  void _updateLocalTask(int id, TaskStatus status) {
    final idx = ManagerMockData.tasks.indexWhere((t) => t.id == id && t.type == TaskType.maintenance);
    if (idx != -1) {
      ManagerMockData.tasks[idx].status = status;
    }
  }
}
