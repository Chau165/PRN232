import 'package:dio/dio.dart';
import 'package:intl/intl.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../auth/auth_state.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class UtilityReadingService {
  final Dio _dio = DioClient.instance;
  final DateFormat _monthFormat = DateFormat('yyyy-MM-01');

  /// Lấy danh sách chỉ số điện nước theo hợp đồng
  Future<List<MeterReadingModel>> getReadings({int? contractId}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (contractId != null) queryParams['contractId'] = contractId;

      final response = await _dio.get(
        ApiEndpoints.utilityReadings,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        return list.map((r) => MeterReadingModel.fromJson(r as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? ManagerMockData.meterReadings : [];
    } catch (_) {
      return AuthState.isCurrentDemo ? ManagerMockData.meterReadings : [];
    }
  }

  /// Ghi chỉ số chốt điện nước định kỳ cho phòng
  Future<void> recordReading({
    required int contractId,
    required DateTime readingMonth,
    required num electricEnd,
    required num waterEnd,
    num? electricStart,
    num? waterStart,
    int? roomId,
  }) async {
    try {
      await _dio.post(
        ApiEndpoints.utilityReadings,
        data: {
          'contractId': contractId,
          'readingMonth': _monthFormat.format(readingMonth),
          'electricEnd': electricEnd,
          'waterEnd': waterEnd,
          if (electricStart != null) 'electricStart': electricStart,
          if (waterStart != null) 'waterStart': waterStart,
        },
      );
      _updateLocalMeter(roomId ?? 0, electricEnd, waterEnd);
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Ghi chỉ số điện nước thất bại.');
    } catch (_) {
      _updateLocalMeter(roomId ?? 0, electricEnd, waterEnd);
    }
  }

  void _updateLocalMeter(int roomId, num newElec, num newWater) {
    final idx = ManagerMockData.meterReadings.indexWhere((m) => m.roomId == roomId);
    if (idx != -1) {
      final old = ManagerMockData.meterReadings[idx];
      old.newElectricity = newElec;
      old.newWater = newWater;
      old.recordDate = DateTime.now();
      old.isRecorded = true;
    }
  }
}
