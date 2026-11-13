import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../auth/auth_state.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class MyRentalData {
  final CustomerContractModel contract;
  final RoomItem room;
  final List<EquipmentItem> equipments;
  final List<AmenityItem> amenities;

  const MyRentalData({
    required this.contract,
    required this.room,
    required this.equipments,
    required this.amenities,
  });
}

class RentalService {
  final Dio _dio = DioClient.instance;

  /// Lấy thông tin phòng và hợp đồng đang thuê của khách hiện tại
  Future<MyRentalData?> getMyActiveRental() async {
    try {
      final response = await _dio.get(ApiEndpoints.myRentals);
      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          final first = list.first as Map<String, dynamic>;

          final contract = CustomerContractModel(
            id: first['contractId'] ?? 0,
            contractCode: 'HD-${first['contractId'] ?? 0}',
            propertyId: first['propertyId'] ?? 0,
            propertyName: first['propertyName'] ?? 'Nhà trọ',
            propertyAddress: first['propertyAddress'] ?? '',
            roomId: first['roomId'] ?? 0,
            roomCode: first['roomCode'] ?? '',
            roomTypeName: first['roomTypeName'] ?? 'Tiêu chuẩn',
            primaryTenantName: 'Bạn (Khách thuê)',
            tenantCccd: '',
            tenantPhone: '',
            startDate: DateTime.tryParse(first['startDate']?.toString() ?? '') ?? DateTime.now(),
            endDate: DateTime.tryParse(first['endDate']?.toString() ?? '') ?? DateTime.now().add(const Duration(days: 365)),
            monthlyRent: first['monthlyRent'] ?? 0,
            depositAmount: first['depositAmount'] ?? 0,
            paymentCycleMonths: 1,
            terms: 'Hợp đồng thuê phòng chính thức',
            status: first['contractStatus'] ?? 'Active',
            billingTimeline: const [],
          );

          final room = RoomItem(
            roomId: first['roomId'] ?? 0,
            roomCode: first['roomCode'] ?? '',
            typeName: first['roomTypeName'] ?? 'Tiêu chuẩn',
            price: first['monthlyRent'] ?? 0,
            depositAmount: first['depositAmount'] ?? 0,
            area: (first['areaSqm'] ?? 20).toDouble(),
            maxOccupancy: 2,
            status: first['roomStatus'] ?? 'Occupied',
            images: (first['images'] as List?)?.map((e) => e.toString()).toList() ??
                (first['primaryImageUrl'] != null ? [first['primaryImageUrl'].toString()] : <String>[]),
            amenities: (first['amenities'] as List?)
                    ?.map((a) => (a is Map ? a['name'] : a).toString())
                    .toList() ??
                [],
            description: 'Phòng trọ đang thuê tại ${first['propertyName'] ?? ''}',
          );

          final equipments = (first['equipments'] as List?)
                  ?.map((e) => EquipmentItem.fromJson(e as Map<String, dynamic>))
                  .toList() ??
              [];

          final amenities = (first['amenities'] as List?)
                  ?.map((a) => AmenityItem.fromJson(a as Map<String, dynamic>))
                  .toList() ??
              [];

          return MyRentalData(
            contract: contract,
            room: room,
            equipments: equipments,
            amenities: amenities,
          );
        }
      }
      return AuthState.isCurrentDemo ? _fallbackMock() : null;
    } catch (_) {
      return AuthState.isCurrentDemo ? _fallbackMock() : null;
    }
  }

  MyRentalData _fallbackMock() {
    return MyRentalData(
      contract: CustomerMockData.contracts.first,
      room: CustomerMockData.myCurrentRoom,
      equipments: CustomerMockData.myEquipments,
      amenities: CustomerMockData.properties.first.amenities,
    );
  }
}
