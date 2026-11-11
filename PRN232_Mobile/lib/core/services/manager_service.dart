import 'package:dio/dio.dart';
import 'package:latlong2/latlong.dart';
import '../../features/customer/models/customer_models.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class ManagerService {
  final Dio _dio = DioClient.instance;

  /// Lấy danh sách khu trọ mà Manager đang được phân công quản lý
  Future<List<PropertyItem>> getMyManagedProperties() async {
    try {
      final response = await _dio.get(ApiEndpoints.propertiesMyManaged);
      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((p) => PropertyItem.fromJson(p as Map<String, dynamic>)).toList();
        }
      }
      return [
        PropertyItem(
          propertyId: 1,
          propertyName: ManagerMockData.currentPropertyName,
          addressText: ManagerMockData.currentPropertyAddress,
          ward: 'Phường Bến Nghé',
          district: 'Quận 1',
          city: 'TP. Hồ Chí Minh',
          location: const LatLng(10.7769, 106.7009),
          availableRoomCount: 6,
          startingPrice: 3500000,
          description: 'Toà nhà trung tâm, an ninh nghiêm ngặt.',
          amenities: const [],
          livingCosts: const [],
          rooms: const [],
          galleryUrls: const [],
        )
      ];
    } catch (_) {
      return [
        PropertyItem(
          propertyId: 1,
          propertyName: ManagerMockData.currentPropertyName,
          addressText: ManagerMockData.currentPropertyAddress,
          ward: 'Phường Bến Nghé',
          district: 'Quận 1',
          city: 'TP. Hồ Chí Minh',
          location: const LatLng(10.7769, 106.7009),
          availableRoomCount: 6,
          startingPrice: 3500000,
          description: 'Toà nhà trung tâm, an ninh nghiêm ngặt.',
          amenities: const [],
          livingCosts: const [],
          rooms: const [],
          galleryUrls: const [],
        )
      ];
    }
  }

  /// Lấy danh sách phòng thuộc khu trọ cho Manager
  Future<List<ManagerRoomModel>> getRooms({int? propertyId}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (propertyId != null) queryParams['propertyId'] = propertyId;

      final response = await _dio.get(
        ApiEndpoints.rooms,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((r) => ManagerRoomModel.fromJson(r as Map<String, dynamic>)).toList();
        }
      }
      return ManagerMockData.rooms;
    } catch (_) {
      return ManagerMockData.rooms;
    }
  }
}
