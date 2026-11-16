import 'package:dio/dio.dart';
import '../auth/auth_state.dart';
import '../constants/app_constants.dart';
import '../storage/secure_storage_helper.dart';

class DioClient {
  static Dio? _instance;

  static Dio get instance {
    _instance ??= _createDio();
    return _instance!;
  }

  static void reset() {
    _instance = null;
  }

  static Dio _createDio() {
    final dio = Dio(
      BaseOptions(
        baseUrl: AppConstants.railwayApiUrl,
        connectTimeout: const Duration(seconds: 30),
        receiveTimeout: const Duration(seconds: 30),
        sendTimeout: const Duration(seconds: 30),
        headers: {
          'Content-Type': 'application/json',
          'Accept': 'application/json',
        },
      ),
    );

    dio.interceptors.add(
      InterceptorsWrapper(
        onRequest: (options, handler) async {
          // Chuẩn hóa path luôn có tiền tố /api khi gọi lên backend
          if (!options.path.startsWith('/api') && !options.path.startsWith('http')) {
            final cleanPath = options.path.startsWith('/') ? options.path : '/${options.path}';
            options.path = '/api$cleanPath';
          }

          // Lấy token ưu tiên từ bộ nhớ authState trước, sau đó từ SecureStorage
          final token = AuthState.instance.currentUser?.token ?? await SecureStorageHelper.getToken();
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          return handler.next(options);
        },
        onResponse: (response, handler) {
          return handler.next(response);
        },
        onError: (DioException error, handler) {
          // Xử lý thông điệp lỗi ProblemDetails từ ASP.NET Core & lỗi mạng
          String message = 'Đã có lỗi xảy ra. Vui lòng thử lại.';
          final statusCode = error.response?.statusCode;

          if (error.type == DioExceptionType.connectionError ||
              error.type == DioExceptionType.connectionTimeout ||
              error.type == DioExceptionType.sendTimeout ||
              error.type == DioExceptionType.receiveTimeout) {
            message = 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra kết nối Wi-Fi/4G của bạn.';
          } else if (error.response?.data is Map) {
            final data = error.response!.data as Map;
            if (data.containsKey('detail') && data['detail'] != null) {
              message = data['detail'].toString();
            } else if (data.containsKey('title') && data['title'] != null) {
              message = data['title'].toString();
            } else if (data.containsKey('message') && data['message'] != null) {
              message = data['message'].toString();
            }
          } else if (statusCode == 401) {
            if (error.requestOptions.path.contains('/auth/login')) {
              message = 'Email hoặc mật khẩu không chính xác.';
            } else {
              message = 'Phiên đăng nhập đã hết hạn hoặc không hợp lệ. Vui lòng đăng nhập lại.';
            }
          } else if (statusCode == 502 || statusCode == 503) {
            message = 'Hệ thống máy chủ tạm thời không khả dụng. Vui lòng thử lại sau ít giây.';
          }

          // Dịch các thông báo phổ biến sang tiếng Việt
          if (message.contains('already have an open viewing request')) {
            message = 'Bạn đã có một lịch hẹn xem đang chờ xử lý cho phòng này. Vui lòng chọn phòng khác hoặc đợi xác nhận.';
          } else if (message.contains('not available for viewing')) {
            message = 'Phòng hiện không khả dụng để hẹn xem.';
          } else if (message.contains('must be in the future')) {
            message = 'Thời gian hẹn xem phòng phải ở tương lai.';
          } else if (message.contains('Room does not exist')) {
            message = 'Phòng không tồn tại.';
          }

          return handler.next(
            DioException(
              requestOptions: error.requestOptions,
              response: error.response,
              type: error.type,
              error: message,
            ),
          );
        },
      ),
    );

    return dio;
  }
}
