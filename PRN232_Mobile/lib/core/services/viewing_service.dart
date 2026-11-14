import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../auth/auth_state.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class ViewingService {
  final Dio _dio = DioClient.instance;

  /// Lấy danh sách lịch hẹn xem phòng cho khách thuê
  Future<List<CustomerViewingModel>> getCustomerViewings({String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.viewingSchedules,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        return list.map((v) => CustomerViewingModel.fromJson(v as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? CustomerMockData.viewings : [];
    } catch (e) {
      if (!AuthState.isCurrentDemo) {
        rethrow;
      }
      return CustomerMockData.viewings;
    }
  }

  /// Lấy danh sách lịch hẹn xem phòng cho Manager
  Future<List<ManagerTaskModel>> getManagerViewings({String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.viewingSchedules,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        return list.map((v) => ManagerTaskModel.fromViewingJson(v as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? ManagerMockData.tasks.where((t) => t.type == TaskType.viewing).toList() : [];
    } catch (e) {
      if (!AuthState.isCurrentDemo) {
        rethrow;
      }
      return ManagerMockData.tasks.where((t) => t.type == TaskType.viewing).toList();
    }
  }

  /// Khách đặt lịch hẹn xem phòng
  Future<CustomerViewingModel> createViewing({
    required int roomId,
    required DateTime scheduledAt,
    String? note,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.viewingSchedules,
        data: {
          'roomId': roomId,
          'scheduledAt': scheduledAt.toUtc().toIso8601String(),
        },
      );

      final data = response.data as Map<String, dynamic>;
      final viewing = CustomerViewingModel.fromJson(data);
      CustomerMockData.viewings.insert(0, viewing);
      return viewing;
    } on DioException catch (e) {
      final msg = e.error?.toString() ??
          (e.response?.data is Map && e.response?.data['detail'] != null
              ? e.response?.data['detail'].toString()
              : 'Đặt lịch xem phòng không thành công.');
      throw Exception(msg);
    } catch (e) {
      if (!AuthState.isCurrentDemo) {
        rethrow;
      }
      final newViewing = CustomerViewingModel(
        id: DateTime.now().millisecondsSinceEpoch % 10000,
        propertyId: 1,
        propertyName: 'Nhà trọ BlueSky Central',
        propertyAddress: '123 Nguyễn Thị Minh Khai',
        roomId: roomId,
        roomCode: 'P.201',
        viewingDate: scheduledAt,
        slotTime: '${scheduledAt.hour.toString().padLeft(2, '0')}:${scheduledAt.minute.toString().padLeft(2, '0')}',
        note: note,
        status: 'Pending',
      );
      CustomerMockData.viewings.insert(0, newViewing);
      return newViewing;
    }
  }

  /// Xác nhận duyệt lịch hẹn xem phòng
  Future<void> approveViewing(int id, {int? assignedTo, DateTime? scheduledAt, String? note}) async {
    try {
      await _dio.post(
        ApiEndpoints.approveViewing(id),
        data: {
          if (assignedTo != null) 'assignedTo': assignedTo,
          if (scheduledAt != null) 'scheduledAt': scheduledAt.toUtc().toIso8601String(),
          if (note != null) 'managerNote': note,
        },
      );
      _updateLocalTaskStatus(id, TaskStatus.approved);
    } catch (_) {
      _updateLocalTaskStatus(id, TaskStatus.approved);
    }
  }

  /// Từ chối lịch hẹn xem phòng
  Future<void> rejectViewing(int id, {String? reason}) async {
    try {
      await _dio.post(
        ApiEndpoints.rejectViewing(id),
        data: {'reason': reason ?? 'Phòng hiện đã có khách xem trùng giờ'},
      );
      _updateLocalTaskStatus(id, TaskStatus.rejected);
    } catch (_) {
      _updateLocalTaskStatus(id, TaskStatus.rejected);
    }
  }

  void _updateLocalTaskStatus(int id, TaskStatus status) {
    final idx = ManagerMockData.tasks.indexWhere((t) => t.id == id && t.type == TaskType.viewing);
    if (idx != -1) {
      ManagerMockData.tasks[idx].status = status;
    }
  }
}
