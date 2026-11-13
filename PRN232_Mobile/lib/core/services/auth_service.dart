import 'package:dio/dio.dart';
import '../auth/user_model.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class AuthService {
  final Dio _dio = DioClient.instance;

  Future<UserModel> login(String email, String password) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.login,
        data: {
          'email': email.trim(),
          'password': password,
        },
      );

      final data = response.data as Map<String, dynamic>;
      final token = data['accessToken']?.toString() ?? '';
      return UserModel.fromJson(data, token: token);
    } on DioException catch (e) {
      final errorMsg = e.error?.toString() ?? 'Đăng nhập không thành công.';
      throw Exception(errorMsg);
    } catch (e) {
      throw Exception('Lỗi kết nối máy chủ: $e');
    }
  }

  Future<UserModel> register({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.register,
        data: {
          'fullName': fullName.trim(),
          'email': email.trim(),
          'phoneNumber': phoneNumber.trim(),
          'password': password,
        },
      );

      final data = response.data as Map<String, dynamic>;
      final token = data['accessToken']?.toString() ?? '';
      return UserModel.fromJson(data, token: token);
    } on DioException catch (e) {
      final errorMsg = e.error?.toString() ?? 'Đăng ký không thành công.';
      throw Exception(errorMsg);
    } catch (e) {
      throw Exception('Lỗi kết nối máy chủ: $e');
    }
  }

  Future<UserModel> getMe() async {
    try {
      final response = await _dio.get(ApiEndpoints.userMe);
      final data = response.data as Map<String, dynamic>;
      return UserModel.fromJson(data);
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Không thể tải thông tin cá nhân.');
    }
  }

  Future<UserModel> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) async {
    try {
      final response = await _dio.put(
        ApiEndpoints.userMe,
        data: {
          'fullName': fullName.trim(),
          'phoneNumber': phoneNumber.trim(),
        },
      );
      final data = response.data as Map<String, dynamic>;
      return UserModel.fromJson(data);
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Không thể cập nhật hồ sơ.');
    }
  }

  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      await _dio.put(
        ApiEndpoints.passwordMe,
        data: {
          'currentPassword': currentPassword,
          'newPassword': newPassword,
        },
      );
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Đổi mật khẩu thất bại.');
    }
  }
}
