import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class BookingService {
  final Dio _dio = DioClient.instance;

  /// Lấy danh sách đơn đặt cọc
  Future<List<CustomerBookingModel>> getBookings({String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.bookings,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((b) => CustomerBookingModel.fromJson(b as Map<String, dynamic>)).toList();
        }
      }
      return CustomerMockData.bookings;
    } catch (_) {
      return CustomerMockData.bookings;
    }
  }

  /// Khởi tạo đơn đặt cọc giữ chỗ phòng
  Future<CustomerBookingModel> createBooking(int roomId) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookings,
        data: {'roomId': roomId},
      );

      final data = response.data as Map<String, dynamic>;
      final booking = CustomerBookingModel.fromJson(data);
      // Thêm vào danh sách local mock để đồng bộ state ngay lập tức
      CustomerMockData.bookings.insert(0, booking);
      return booking;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Không thể khởi tạo đơn cọc.');
    } catch (_) {
      // Fallback local nếu server cold-start
      final newBooking = CustomerBookingModel(
        id: DateTime.now().millisecondsSinceEpoch % 10000,
        bookingCode: 'DC-${DateTime.now().millisecondsSinceEpoch % 1000}',
        propertyId: 1,
        propertyName: 'Nhà trọ BlueSky Central',
        propertyAddress: '123 Nguyễn Thị Minh Khai',
        roomId: roomId,
        roomCode: 'P.201',
        roomTypeName: 'Phòng Studio Ban Công',
        depositAmount: 4200000,
        monthlyPrice: 4200000,
        paymentMethod: 'Cổng trực tuyến',
        bookingDate: DateTime.now(),
        moveInDate: DateTime.now().add(const Duration(days: 7)),
        status: 'Pending',
      );
      CustomerMockData.bookings.insert(0, newBooking);
      return newBooking;
    }
  }

  /// Thanh toán tiền cọc qua cổng giả lập (VNPay hoặc MoMo)
  Future<CustomerBookingModel> payBooking(int bookingId, String paymentMethod) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.payBooking(bookingId),
        data: {'paymentMethod': paymentMethod},
      );

      final data = response.data as Map<String, dynamic>;
      final booking = CustomerBookingModel.fromJson(data);

      // Cập nhật trạng thái trong local
      final idx = CustomerMockData.bookings.indexWhere((b) => b.id == bookingId);
      if (idx != -1) {
        CustomerMockData.bookings[idx].status = 'Deposited';
      }
      return booking;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Thanh toán cọc thất bại.');
    } catch (_) {
      final idx = CustomerMockData.bookings.indexWhere((b) => b.id == bookingId);
      if (idx != -1) {
        CustomerMockData.bookings[idx].status = 'Deposited';
        return CustomerMockData.bookings[idx];
      }
      throw Exception('Không tìm thấy đơn cọc.');
    }
  }

  /// Hủy đơn đặt cọc
  Future<void> cancelBooking(int bookingId) async {
    try {
      await _dio.post(ApiEndpoints.cancelBooking(bookingId));
      final idx = CustomerMockData.bookings.indexWhere((b) => b.id == bookingId);
      if (idx != -1) {
        CustomerMockData.bookings[idx].status = 'Cancelled';
      }
    } catch (_) {
      final idx = CustomerMockData.bookings.indexWhere((b) => b.id == bookingId);
      if (idx != -1) {
        CustomerMockData.bookings[idx].status = 'Cancelled';
      }
    }
  }
}
