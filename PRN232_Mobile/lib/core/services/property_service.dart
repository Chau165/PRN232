import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class PropertyService {
  final Dio _dio = DioClient.instance;

  /// Lấy danh sách toà nhà trọ công khai (hỗ trợ filter, search, paging)
  Future<List<PropertyItem>> getPublicProperties({
    String? keyword,
    String? city,
    String? district,
    String? ward,
    num? minPrice,
    num? maxPrice,
    String? roomTypeName,
    List<int>? amenityIds,
    String? sortBy,
    int page = 1,
    int pageSize = 20,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'Page': page,
        'PageSize': pageSize,
      };
      if (keyword != null && keyword.isNotEmpty) queryParams['Keyword'] = keyword;
      if (city != null && city.isNotEmpty) queryParams['City'] = city;
      if (district != null && district.isNotEmpty) queryParams['District'] = district;
      if (ward != null && ward.isNotEmpty) queryParams['Ward'] = ward;
      if (minPrice != null) queryParams['MinPrice'] = minPrice;
      if (maxPrice != null) queryParams['MaxPrice'] = maxPrice;
      if (roomTypeName != null && roomTypeName.isNotEmpty) queryParams['RoomTypeName'] = roomTypeName;
      if (amenityIds != null && amenityIds.isNotEmpty) queryParams['AmenityIds'] = amenityIds;
      if (sortBy != null && sortBy.isNotEmpty) queryParams['SortBy'] = sortBy;

      final response = await _dio.get(
        ApiEndpoints.publicProperties,
        queryParameters: queryParams,
      );

      final data = response.data;
      if (data is Map && data.containsKey('items') && data['items'] is List) {
        final items = data['items'] as List;
        if (items.isNotEmpty) {
          return items.map((i) => PropertyItem.fromJson(i as Map<String, dynamic>)).toList();
        }
      }
      return CustomerMockData.properties;
    } catch (_) {
      // Graceful fallback: trả về dữ liệu mẫu nếu API cold-start hoặc lỗi mạng
      return CustomerMockData.properties;
    }
  }

  /// Lấy danh sách toà nhà có tọa độ GPS để hiển thị ghim trên bản đồ OSM
  Future<List<PropertyItem>> getPropertiesByArea({
    String? city,
    String? district,
    String? ward,
  }) async {
    try {
      final queryParams = <String, dynamic>{};
      if (city != null) queryParams['City'] = city;
      if (district != null) queryParams['District'] = district;
      if (ward != null) queryParams['Ward'] = ward;

      final response = await _dio.get(
        ApiEndpoints.publicPropertiesByArea,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((i) => PropertyItem.fromJson(i as Map<String, dynamic>)).toList();
        }
      }
      return CustomerMockData.properties;
    } catch (_) {
      return CustomerMockData.properties;
    }
  }

  /// Chi tiết tòa nhà trọ (tiện ích chung, bảng giá chi phí sinh hoạt)
  Future<PropertyItem> getPropertyDetail(int propertyId) async {
    try {
      final response = await _dio.get(ApiEndpoints.publicPropertyDetail(propertyId));
      if (response.data is Map<String, dynamic>) {
        final detail = PropertyItem.fromJson(response.data as Map<String, dynamic>);
        // Lấy thêm danh sách phòng của toà nhà này
        try {
          final rooms = await getPropertyRooms(propertyId);
          return PropertyItem(
            propertyId: detail.propertyId,
            propertyName: detail.propertyName,
            addressText: detail.addressText,
            ward: detail.ward,
            district: detail.district,
            city: detail.city,
            location: detail.location,
            primaryImageUrl: detail.primaryImageUrl,
            galleryUrls: detail.galleryUrls,
            availableRoomCount: detail.availableRoomCount,
            startingPrice: detail.startingPrice,
            description: detail.description,
            amenities: detail.amenities,
            livingCosts: detail.livingCosts,
            rooms: rooms.isNotEmpty ? rooms : detail.rooms,
          );
        } catch (_) {
          return detail;
        }
      }
      return CustomerMockData.getPropertyById(propertyId);
    } catch (_) {
      return CustomerMockData.getPropertyById(propertyId);
    }
  }

  /// Lấy danh sách phòng thuộc tòa nhà trọ
  Future<List<RoomItem>> getPropertyRooms(
    int propertyId, {
    num? minPrice,
    num? maxPrice,
    String? roomTypeName,
  }) async {
    try {
      final queryParams = <String, dynamic>{};
      if (minPrice != null) queryParams['MinPrice'] = minPrice;
      if (maxPrice != null) queryParams['MaxPrice'] = maxPrice;
      if (roomTypeName != null) queryParams['RoomTypeName'] = roomTypeName;

      final response = await _dio.get(
        ApiEndpoints.publicPropertyRooms(propertyId),
        queryParameters: queryParams,
      );

      if (response.data is Map && response.data.containsKey('items') && response.data['items'] is List) {
        final list = response.data['items'] as List;
        if (list.isNotEmpty) {
          return list.map((r) => RoomItem.fromJson(r as Map<String, dynamic>)).toList();
        }
      }
      return CustomerMockData.getPropertyById(propertyId).rooms;
    } catch (_) {
      return CustomerMockData.getPropertyById(propertyId).rooms;
    }
  }

  /// Chi tiết cụ thể một phòng
  Future<RoomItem> getRoomDetail(int roomId) async {
    try {
      final response = await _dio.get(ApiEndpoints.publicRoomDetail(roomId));
      if (response.data is Map<String, dynamic>) {
        return RoomItem.fromJson(response.data as Map<String, dynamic>);
      }
      return CustomerMockData.getRoomById(roomId);
    } catch (_) {
      return CustomerMockData.getRoomById(roomId);
    }
  }
}
