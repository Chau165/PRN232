import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../auth/auth_state.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class PublicPropertyPageResponse {
  final List<PropertyItem> items;
  final int totalCount;
  final int page;
  final int pageSize;

  int get totalPages => (totalCount / pageSize).ceil() < 1 ? 1 : (totalCount / pageSize).ceil();

  const PublicPropertyPageResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
  });
}

class PublicPropertyRoomPageResponse {
  final List<RoomItem> items;
  final int totalCount;
  final int page;
  final int pageSize;

  int get totalPages => (totalCount / pageSize).ceil() < 1 ? 1 : (totalCount / pageSize).ceil();

  const PublicPropertyRoomPageResponse({
    required this.items,
    required this.totalCount,
    required this.page,
    required this.pageSize,
  });
}

class PropertyService {
  final Dio _dio = DioClient.instance;

  /// Lấy danh sách toà nhà trọ công khai phân trang (hỗ trợ filter, search, paging)
  Future<PublicPropertyPageResponse> getPublicPropertiesPaged({
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
    int pageSize = 10,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'Page': page,
        'PageSize': pageSize,
      };
      if (keyword != null && keyword.trim().isNotEmpty) queryParams['Keyword'] = keyword.trim();
      if (city != null && city.trim().isNotEmpty) queryParams['City'] = city.trim();
      if (district != null && district.trim().isNotEmpty && district != 'Tất cả') {
        queryParams['District'] = district.trim();
      }
      if (ward != null && ward.trim().isNotEmpty) queryParams['Ward'] = ward.trim();
      if (minPrice != null) queryParams['MinPrice'] = minPrice;
      if (maxPrice != null) queryParams['MaxPrice'] = maxPrice;
      if (roomTypeName != null && roomTypeName.trim().isNotEmpty) queryParams['RoomTypeName'] = roomTypeName.trim();
      if (amenityIds != null && amenityIds.isNotEmpty) queryParams['AmenityIds'] = amenityIds;
      if (sortBy != null && sortBy.trim().isNotEmpty) queryParams['SortBy'] = sortBy.trim();

      final response = await _dio.get(
        ApiEndpoints.publicProperties,
        queryParameters: queryParams,
      );

      final data = response.data;
      if (data is Map && data.containsKey('items') && data['items'] is List) {
        final itemsList = (data['items'] as List)
            .map((i) => PropertyItem.fromJson(i as Map<String, dynamic>))
            .toList();
        final totalCount = (data['totalCount'] as num?)?.toInt() ?? itemsList.length;
        final resPage = (data['page'] as num?)?.toInt() ?? page;
        final resPageSize = (data['pageSize'] as num?)?.toInt() ?? pageSize;

        return PublicPropertyPageResponse(
          items: itemsList,
          totalCount: totalCount,
          page: resPage,
          pageSize: resPageSize,
        );
      }

      return PublicPropertyPageResponse(
        items: CustomerMockData.properties,
        totalCount: CustomerMockData.properties.length,
        page: 1,
        pageSize: pageSize,
      );
    } catch (_) {
      return PublicPropertyPageResponse(
        items: CustomerMockData.properties,
        totalCount: CustomerMockData.properties.length,
        page: 1,
        pageSize: pageSize,
      );
    }
  }

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
    final paged = await getPublicPropertiesPaged(
      keyword: keyword,
      city: city,
      district: district,
      ward: ward,
      minPrice: minPrice,
      maxPrice: maxPrice,
      roomTypeName: roomTypeName,
      amenityIds: amenityIds,
      sortBy: sortBy,
      page: page,
      pageSize: pageSize,
    );
    return paged.items;
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
          final rooms = await getPropertyRooms(propertyId, pageSize: 50);
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
            availableRoomCount: rooms.isNotEmpty ? rooms.length : detail.availableRoomCount,
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

  /// Lấy danh sách phòng thuộc tòa nhà trọ có phân trang (10 phòng / 1 trang)
  Future<PublicPropertyRoomPageResponse> getPropertyRoomsPaged(
    int propertyId, {
    int page = 1,
    int pageSize = 10,
    num? minPrice,
    num? maxPrice,
    String? roomTypeName,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'Page': page,
        'PageSize': pageSize,
      };
      if (minPrice != null) queryParams['MinPrice'] = minPrice;
      if (maxPrice != null) queryParams['MaxPrice'] = maxPrice;
      if (roomTypeName != null) queryParams['RoomTypeName'] = roomTypeName;

      final response = await _dio.get(
        ApiEndpoints.publicPropertyRooms(propertyId),
        queryParameters: queryParams,
      );

      if (response.data is Map && response.data.containsKey('items') && response.data['items'] is List) {
        final list = (response.data['items'] as List)
            .map((r) => RoomItem.fromJson(r as Map<String, dynamic>))
            .toList();
        final totalCount = (response.data['totalCount'] as num?)?.toInt() ?? list.length;
        final resPage = (response.data['page'] as num?)?.toInt() ?? page;
        final resPageSize = (response.data['pageSize'] as num?)?.toInt() ?? pageSize;
        return PublicPropertyRoomPageResponse(
          items: list,
          totalCount: totalCount,
          page: resPage,
          pageSize: resPageSize,
        );
      }
      final mockRooms = CustomerMockData.getPropertyById(propertyId).rooms;
      return PublicPropertyRoomPageResponse(
        items: mockRooms,
        totalCount: mockRooms.length,
        page: 1,
        pageSize: pageSize,
      );
    } catch (_) {
      final mockRooms = CustomerMockData.getPropertyById(propertyId).rooms;
      return PublicPropertyRoomPageResponse(
        items: AuthState.isCurrentDemo ? mockRooms : [],
        totalCount: AuthState.isCurrentDemo ? mockRooms.length : 0,
        page: 1,
        pageSize: pageSize,
      );
    }
  }

  /// Lấy danh sách phòng thuộc tòa nhà trọ
  Future<List<RoomItem>> getPropertyRooms(
    int propertyId, {
    num? minPrice,
    num? maxPrice,
    String? roomTypeName,
    int pageSize = 50,
  }) async {
    try {
      final queryParams = <String, dynamic>{
        'PageSize': pageSize,
      };
      if (minPrice != null) queryParams['MinPrice'] = minPrice;
      if (maxPrice != null) queryParams['MaxPrice'] = maxPrice;
      if (roomTypeName != null) queryParams['RoomTypeName'] = roomTypeName;

      final response = await _dio.get(
        ApiEndpoints.publicPropertyRooms(propertyId),
        queryParameters: queryParams,
      );

      if (response.data is Map && response.data.containsKey('items') && response.data['items'] is List) {
        final list = response.data['items'] as List;
        return list.map((r) => RoomItem.fromJson(r as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? CustomerMockData.getPropertyById(propertyId).rooms : [];
    } catch (_) {
      return AuthState.isCurrentDemo ? CustomerMockData.getPropertyById(propertyId).rooms : [];
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
