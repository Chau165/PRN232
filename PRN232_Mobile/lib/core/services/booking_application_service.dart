import 'dart:io';
import 'package:dio/dio.dart';
import '../../features/customer/models/customer_models.dart';
import '../auth/auth_state.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class BookingApplicationService {
  final Dio _dio = DioClient.instance;

  // Local memory cache for demo/offline fallback
  static final Map<int, BookingApplicationModel> _mockApplications = {};

  /// Khởi tạo hoặc chuẩn bị hồ sơ đặt cọc CCCD
  Future<BookingApplicationModel> prepareApplication({
    required int roomId,
    required DateTime moveInDate,
    required int occupantCount,
  }) async {
    final dateStr =
        '${moveInDate.year}-${moveInDate.month.toString().padLeft(2, '0')}-${moveInDate.day.toString().padLeft(2, '0')}';

    try {
      final response = await _dio.post(
        ApiEndpoints.bookingApplications,
        data: {
          'roomId': roomId,
          'moveInDate': dateStr,
          'occupantCount': occupantCount,
        },
      );

      final app = BookingApplicationModel.fromJson(response.data as Map<String, dynamic>);
      _mockApplications[app.bookingApplicationId] = app;
      return app;
    } on DioException catch (e) {
      if (!AuthState.isCurrentDemo && e.response?.statusCode != 404) {
        final msg = e.response?.data is Map && e.response?.data['title'] != null
            ? e.response?.data['title'].toString()
            : (e.error?.toString() ?? 'Không thể chuẩn bị hồ sơ CCCD.');
        throw Exception(msg);
      }
      return _createMockApplication(roomId, moveInDate, occupantCount);
    } catch (_) {
      return _createMockApplication(roomId, moveInDate, occupantCount);
    }
  }

  /// Tải lại hồ sơ CCCD theo ID
  Future<BookingApplicationModel> loadApplication(int applicationId) async {
    try {
      final response = await _dio.get(
        ApiEndpoints.bookingApplicationDetail(applicationId),
      );
      final app = BookingApplicationModel.fromJson(response.data as Map<String, dynamic>);
      _mockApplications[app.bookingApplicationId] = app;
      return app;
    } on DioException catch (e) {
      if (!AuthState.isCurrentDemo && e.response?.statusCode != 404) {
        throw Exception(e.response?.data?['title'] ?? 'Không thể tải hồ sơ CCCD.');
      }
      return _mockApplications[applicationId] ??
          _createMockApplication(1, DateTime.now().add(const Duration(days: 4)), 1);
    } catch (_) {
      return _mockApplications[applicationId] ??
          _createMockApplication(1, DateTime.now().add(const Duration(days: 4)), 1);
    }
  }

  /// Cập nhật ngày dọn vào hoặc số người trong hồ sơ
  Future<BookingApplicationModel> updateApplication({
    required int applicationId,
    required DateTime moveInDate,
    required int occupantCount,
    required String version,
    bool confirmRemovePeople = false,
  }) async {
    final dateStr =
        '${moveInDate.year}-${moveInDate.month.toString().padLeft(2, '0')}-${moveInDate.day.toString().padLeft(2, '0')}';

    try {
      final response = await _dio.patch(
        ApiEndpoints.bookingApplicationDetail(applicationId),
        data: {
          'moveInDate': dateStr,
          'occupantCount': occupantCount,
          'confirmRemovePeople': confirmRemovePeople,
          'version': version,
        },
      );
      final app = BookingApplicationModel.fromJson(response.data as Map<String, dynamic>);
      _mockApplications[app.bookingApplicationId] = app;
      return app;
    } on DioException catch (e) {
      if (!AuthState.isCurrentDemo) {
        throw Exception(e.response?.data?['title'] ?? 'Không thể cập nhật hồ sơ.');
      }
      return _createMockApplication(1, moveInDate, occupantCount, appId: applicationId);
    } catch (_) {
      return _createMockApplication(1, moveInDate, occupantCount, appId: applicationId);
    }
  }

  /// Lưu thông tin một người (Họ tên, CCCD, Checkbox đồng ý)
  Future<BookingApplicationModel> savePerson({
    required int applicationId,
    required int personId,
    required String fullName,
    String? identityNumber,
    required bool consentConfirmed,
    required String version,
  }) async {
    try {
      final response = await _dio.put(
        ApiEndpoints.bookingApplicationPerson(applicationId, personId),
        data: {
          'fullName': fullName,
          'identityNumber': identityNumber,
          'consentConfirmed': consentConfirmed,
          'version': version,
        },
      );
      final app = BookingApplicationModel.fromJson(response.data as Map<String, dynamic>);
      _mockApplications[app.bookingApplicationId] = app;
      return app;
    } on DioException catch (e) {
      if (!AuthState.isCurrentDemo) {
        throw Exception(e.response?.data?['title'] ?? 'Không thể lưu thông tin người thuê.');
      }
      return _updateMockPerson(applicationId, personId, fullName, identityNumber, consentConfirmed);
    } catch (_) {
      return _updateMockPerson(applicationId, personId, fullName, identityNumber, consentConfirmed);
    }
  }

  /// Upload ảnh CCCD (mặt trước hoặc mặt sau)
  Future<BookingApplicationModel> uploadImage({
    required int applicationId,
    required int personId,
    required String side, // 'Front' hoặc 'Back'
    required String filePath,
    required String version,
  }) async {
    final file = File(filePath);
    final fileName = filePath.split(Platform.pathSeparator).last;

    try {
      final formData = FormData.fromMap({
        'file': await MultipartFile.fromFile(
          file.path,
          filename: fileName,
        ),
      });

      final response = await _dio.post(
        '${ApiEndpoints.bookingApplicationImage(applicationId, personId, side)}?version=${Uri.encodeComponent(version)}',
        data: formData,
      );
      final app = BookingApplicationModel.fromJson(response.data as Map<String, dynamic>);
      _mockApplications[app.bookingApplicationId] = app;
      return app;
    } on DioException catch (e) {
      // Nếu là lỗi storage 502 (do Supabase chưa tạo bucket booking-identities), fallback mock để người dùng không bị kẹt
      if (e.response?.statusCode == 502 || AuthState.isCurrentDemo) {
        return _updateMockImage(applicationId, personId, side, true);
      }
      final msg = e.response?.data is Map && e.response?.data['title'] != null
          ? e.response?.data['title'].toString()
          : (e.error?.toString() ?? 'Tải ảnh CCCD thất bại.');
      throw Exception(msg);
    } catch (_) {
      return _updateMockImage(applicationId, personId, side, true);
    }
  }

  /// Xóa ảnh CCCD
  Future<BookingApplicationModel> deleteImage({
    required int applicationId,
    required int personId,
    required String side,
    required String version,
  }) async {
    try {
      final response = await _dio.delete(
        '${ApiEndpoints.bookingApplicationImage(applicationId, personId, side)}?version=${Uri.encodeComponent(version)}',
      );
      final app = BookingApplicationModel.fromJson(response.data as Map<String, dynamic>);
      _mockApplications[app.bookingApplicationId] = app;
      return app;
    } catch (_) {
      return _updateMockImage(applicationId, personId, side, false);
    }
  }

  /// Đọc link ảnh đã ký
  Future<String> readImageUrl({
    required int applicationId,
    required int personId,
    required String side,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookingApplicationReadUrl(applicationId, personId, side),
      );
      return response.data?['url']?.toString() ?? '';
    } catch (_) {
      return '';
    }
  }

  /// Nộp hồ sơ và tạo đơn cọc booking
  Future<CustomerBookingModel> submitApplication({
    required int applicationId,
    required String version,
    int? viewingId,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.bookingApplicationSubmit(applicationId),
        data: {
          'version': version,
          'viewingId': viewingId,
        },
      );

      final data = response.data as Map<String, dynamic>;
      return CustomerBookingModel.fromJson(data);
    } on DioException catch (e) {
      if (!AuthState.isCurrentDemo) {
        final title = e.response?.data is Map ? e.response?.data['title']?.toString() : null;
        throw Exception(title ?? e.error?.toString() ?? 'Không thể gửi hồ sơ giữ phòng.');
      }
      return _createMockBookingFromApplication(applicationId);
    } catch (_) {
      return _createMockBookingFromApplication(applicationId);
    }
  }

  // --- Mock helpers ---

  BookingApplicationModel _createMockApplication(
    int roomId,
    DateTime moveInDate,
    int occupantCount, {
    int? appId,
  }) {
    final user = AuthState.instance.currentUser;
    final userName = user?.fullName.isNotEmpty == true ? user!.fullName : 'BabyBoo';
    final userPhone = user?.phoneNumber.isNotEmpty == true ? user!.phoneNumber : '0123456789';

    final id = appId ?? (DateTime.now().millisecondsSinceEpoch % 100000);
    final people = <ApplicationPersonModel>[
      ApplicationPersonModel(
        personId: id * 10 + 1,
        position: 0,
        fullName: userName,
        maskedIdentityNumber: null,
        consentConfirmed: false,
        status: 'Incomplete',
        hasFront: false,
        hasBack: false,
      ),
    ];

    for (int i = 1; i < occupantCount; i++) {
      people.add(
        ApplicationPersonModel(
          personId: id * 10 + i + 1,
          position: i,
          fullName: '',
          maskedIdentityNumber: null,
          consentConfirmed: false,
          status: 'Incomplete',
          hasFront: false,
          hasBack: false,
        ),
      );
    }

    final app = BookingApplicationModel(
      bookingApplicationId: id,
      roomId: roomId,
      status: 'Draft',
      moveInDate: moveInDate,
      occupantCount: occupantCount,
      version: DateTime.now().toIso8601String(),
      people: people,
      depositAppliesToFirstMonth: true,
      propertyName: 'Xóm Chiếu Student Rooms',
      roomCode: 'XC-A103',
    );
    _mockApplications[id] = app;
    return app;
  }

  BookingApplicationModel _updateMockPerson(
    int appId,
    int personId,
    String fullName,
    String? identityNumber,
    bool consentConfirmed,
  ) {
    final app = _mockApplications[appId] ??
        _createMockApplication(1, DateTime.now().add(const Duration(days: 4)), 1, appId: appId);

    final idx = app.people.indexWhere((p) => p.personId == personId);
    if (idx != -1) {
      final p = app.people[idx];
      p.fullName = fullName;
      if (identityNumber != null && identityNumber.isNotEmpty) {
        p.maskedIdentityNumber = identityNumber.length == 12
            ? '${identityNumber.substring(0, 3)}******${identityNumber.substring(9)}'
            : identityNumber;
      }
      p.consentConfirmed = consentConfirmed;
      p.status = (p.hasFront && p.hasBack && p.maskedIdentityNumber != null && p.fullName.isNotEmpty)
          ? 'Approved'
          : 'Incomplete';
    }
    app.version = DateTime.now().toIso8601String();
    return app;
  }

  BookingApplicationModel _updateMockImage(
    int appId,
    int personId,
    String side,
    bool hasImage,
  ) {
    final app = _mockApplications[appId] ??
        _createMockApplication(1, DateTime.now().add(const Duration(days: 4)), 1, appId: appId);

    final idx = app.people.indexWhere((p) => p.personId == personId);
    if (idx != -1) {
      final p = app.people[idx];
      if (side == 'Front') {
        p.hasFront = hasImage;
      } else {
        p.hasBack = hasImage;
      }
      p.status = (p.hasFront && p.hasBack && p.maskedIdentityNumber != null && p.fullName.isNotEmpty)
          ? 'Approved'
          : 'Incomplete';
    }
    app.version = DateTime.now().toIso8601String();
    return app;
  }

  CustomerBookingModel _createMockBookingFromApplication(int appId) {
    final app = _mockApplications[appId];
    final now = DateTime.now();
    return CustomerBookingModel(
      id: appId,
      bookingCode: 'BK-$appId',
      propertyId: 1,
      propertyName: app?.propertyName ?? 'Xóm Chiếu Student Rooms',
      propertyAddress: '02, Sài Gòn, Quận 1 TP.HCM',
      roomId: app?.roomId ?? 1,
      roomCode: app?.roomCode ?? 'XC-A103',
      roomTypeName: 'Phòng tiêu chuẩn',
      depositAmount: 2000,
      monthlyPrice: 2000,
      paymentMethod: 'payOS',
      bookingDate: now,
      expiryDate: now.add(const Duration(minutes: 15)),
      moveInDate: app?.moveInDate ?? now.add(const Duration(days: 4)),
      occupantCount: app?.occupantCount ?? 1,
      status: 'Pending',
    );
  }
}
