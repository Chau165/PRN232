import 'package:dio/dio.dart';
import 'package:intl/intl.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../auth/auth_state.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class EquipmentService {
  final Dio _dio = DioClient.instance;
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');

  /// Lấy danh sách thiết bị kiểm kê của phòng hoặc toà nhà
  Future<List<ManagerEquipmentModel>> getEquipments({int? roomId}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (roomId != null) queryParams['roomId'] = roomId;

      final response = await _dio.get(
        ApiEndpoints.equipments,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        return list.map((e) => ManagerEquipmentModel.fromJson(e as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? ManagerMockData.equipments : [];
    } catch (_) {
      return AuthState.isCurrentDemo ? ManagerMockData.equipments : [];
    }
  }

  /// Thêm thiết bị mới
  Future<ManagerEquipmentModel> createEquipment({
    required int roomId,
    required String name,
    required DateTime installDate,
    required DateTime warrantyExpiry,
    required String supplier,
    required String status,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.equipments,
        data: {
          'roomId': roomId,
          'name': name.trim(),
          'installDate': _dateFormat.format(installDate),
          'warrantyExpiry': _dateFormat.format(warrantyExpiry),
          'supplier': supplier.trim(),
          'status': status,
        },
      );

      final data = response.data as Map<String, dynamic>;
      final eq = ManagerEquipmentModel.fromJson(data);
      ManagerMockData.equipments.insert(0, eq);
      return eq;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Thêm thiết bị thất bại.');
    } catch (_) {
      final newEq = ManagerEquipmentModel(
        id: DateTime.now().millisecondsSinceEpoch % 10000,
        code: 'TB-${DateTime.now().millisecondsSinceEpoch % 1000}',
        name: name,
        roomCode: 'P.101',
        category: 'Gia dụng',
        status: status,
        installedDate: _dateFormat.format(installDate),
      );
      ManagerMockData.equipments.insert(0, newEq);
      return newEq;
    }
  }

  /// Cập nhật tình trạng thiết bị (Tốt, Cần sửa, Hỏng)
  Future<void> updateStatus(int id, String newStatus) async {
    try {
      await _dio.put(
        ApiEndpoints.equipmentDetail(id),
        data: {
          'status': newStatus,
          'name': 'Thiết bị',
          'installDate': _dateFormat.format(DateTime.now()),
          'warrantyExpiry': _dateFormat.format(DateTime.now().add(const Duration(days: 365))),
          'supplier': 'Chính hãng',
        },
      );
      _updateLocalEq(id, newStatus);
    } catch (_) {
      _updateLocalEq(id, newStatus);
    }
  }

  void _updateLocalEq(int id, String status) {
    final idx = ManagerMockData.equipments.indexWhere((e) => e.id == id);
    if (idx != -1) {
      ManagerMockData.equipments[idx].status = status;
    }
  }
}
