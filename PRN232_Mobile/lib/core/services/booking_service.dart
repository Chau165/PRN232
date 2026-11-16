import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../auth/auth_state.dart';
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
        return list.map((b) => CustomerBookingModel.fromJson(b as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? CustomerMockData.bookings : [];
    } catch (e) {
      if (!AuthState.isCurrentDemo) {
        rethrow;
      }
      return CustomerMockData.bookings;
    }
  }

  /// Khởi tạo đơn đặt cọc giữ chỗ phòng với ngày dọn vào và số người ở
  Future<CustomerBookingModel> createBooking(
    int roomId, {
    DateTime? moveInDate,
    int? occupantCount,
    int? viewingId,
  }) async {
    try {
      final payload = <String, dynamic>{'roomId': roomId};
      if (moveInDate != null) {
        payload['moveInDate'] =
            '${moveInDate.year}-${moveInDate.month.toString().padLeft(2, '0')}-${moveInDate.day.toString().padLeft(2, '0')}';
      }
      if (occupantCount != null) {
        payload['occupantCount'] = occupantCount;
      }
      if (viewingId != null) {
        payload['viewingId'] = viewingId;
      }

      final response = await _dio.post(
        ApiEndpoints.bookings,
        data: payload,
      );

      final data = response.data as Map<String, dynamic>;
      final booking = CustomerBookingModel.fromJson(data);
      // Thêm vào danh sách local mock để đồng bộ state ngay lập tức
      CustomerMockData.bookings.insert(0, booking);
      return booking;
    } on DioException catch (e) {
      final msg = e.response?.data is Map && e.response?.data['detail'] != null
          ? e.response?.data['detail'].toString()
          : (e.error?.toString() ?? 'Không thể khởi tạo đơn cọc.');
      throw Exception(msg);
    } catch (e) {
      if (!AuthState.isCurrentDemo) {
        rethrow;
      }
      final now = DateTime.now();
      // Fallback local chỉ dành cho chế độ Demo
      final newBooking = CustomerBookingModel(
        id: now.millisecondsSinceEpoch % 10000,
        bookingCode: 'DC-${now.millisecondsSinceEpoch % 1000}',
        propertyId: 1,
        propertyName: 'Saigon Student House',
        propertyAddress: '02, Sài Gòn, Khu vực Quận 1 TP.HCM',
        roomId: roomId,
        roomCode: 'SS-A104',
        roomTypeName: 'Phòng tiêu chuẩn',
        depositAmount: 2000,
        monthlyPrice: 2000,
        paymentMethod: 'payOS',
        bookingDate: now,
        expiryDate: now.add(const Duration(minutes: 15)),
        moveInDate: moveInDate ?? now.add(const Duration(days: 4)),
        occupantCount: occupantCount ?? 2,
        status: 'Pending',
      );
      CustomerMockData.bookings.insert(0, newBooking);
      return newBooking;
    }
  }

  /// Lấy chi tiết một booking
  Future<CustomerBookingModel> getBookingDetail(int bookingId) async {
    try {
      final response = await _dio.get(ApiEndpoints.bookingDetail(bookingId));
      return CustomerBookingModel.fromJson(response.data as Map<String, dynamic>);
    } catch (_) {
      final found = CustomerMockData.bookings.where((b) => b.id == bookingId);
      if (found.isNotEmpty) return found.first;
      rethrow;
    }
  }

  /// Tạo link thanh toán cọc qua payOS
  Future<BookingPaymentLinkModel> createPaymentLink(int bookingId) async {
    try {
      final response = await _dio.post(ApiEndpoints.bookingPaymentLink(bookingId));
      final data = response.data as Map<String, dynamic>;
      return BookingPaymentLinkModel.fromJson(data);
    } on DioException catch (e) {
      // Nếu backend trả về 503 (chưa bật payOS thật) hoặc lỗi, fallback sang mock payOS link
      if (e.response?.statusCode == 503 || AuthState.isCurrentDemo) {
        return _fallbackMockPaymentLink(bookingId);
      }
      final msg = e.response?.data is Map && e.response?.data['detail'] != null
          ? e.response?.data['detail'].toString()
          : 'Không thể tạo liên kết thanh toán payOS.';
      throw Exception(msg);
    } catch (_) {
      return _fallbackMockPaymentLink(bookingId);
    }
  }

  BookingPaymentLinkModel _fallbackMockPaymentLink(int bookingId) {
    num deposit = 2000;
    final b = CustomerMockData.bookings.where((item) => item.id == bookingId);
    if (b.isNotEmpty) deposit = b.first.depositAmount;

    return BookingPaymentLinkModel(
      paymentRequestId: (bookingId * 3) + 7,
      bookingId: bookingId,
      status: 'Pending',
      amount: deposit,
      currency: 'VND',
      checkoutUrl: 'https://pay.payos.vn/web/mock-booking-$bookingId',
      expiresAtUtc: DateTime.now().add(const Duration(minutes: 15)),
      reused: false,
    );
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
      final msg = e.response?.data is Map && e.response?.data['detail'] != null
          ? e.response?.data['detail'].toString()
          : (e.error?.toString() ?? 'Thanh toán cọc thất bại.');
      throw Exception(msg);
    } catch (e) {
      if (!AuthState.isCurrentDemo) {
        rethrow;
      }
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

  /// Lấy trạng thái của PaymentRequest từ payOS (polling / kiểm tra kết quả thanh toán)
  Future<PaymentRequestStatusModel> getPaymentRequestStatus(int requestId) async {
    try {
      final response = await _dio.get(ApiEndpoints.paymentRequestStatus(requestId));
      final data = response.data as Map<String, dynamic>;
      return PaymentRequestStatusModel.fromJson(data);
    } on DioException catch (e) {
      if (!AuthState.isCurrentDemo && e.response?.statusCode != 404) {
        final msg = e.response?.data is Map && e.response?.data['detail'] != null
            ? e.response?.data['detail'].toString()
            : (e.error?.toString() ?? 'Chưa thể xác nhận kết quả với máy chủ.');
        throw Exception(msg);
      }
      return _mockPaymentRequestStatus(requestId);
    } catch (_) {
      return _mockPaymentRequestStatus(requestId);
    }
  }

  PaymentRequestStatusModel _mockPaymentRequestStatus(int requestId) {
    return PaymentRequestStatusModel(
      paymentRequestId: requestId,
      customerId: 1,
      propertyId: 1,
      status: 'Pending',
      bookingStatus: 'Pending',
      amount: 2000,
      createdAtUtc: DateTime.now().subtract(const Duration(minutes: 1)),
      updatedAtUtc: DateTime.now(),
      expiresAtUtc: DateTime.now().add(const Duration(minutes: 14)),
      canOpenCheckout: true,
    );
  }
}
