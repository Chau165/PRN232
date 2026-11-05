import 'package:dio/dio.dart';
import '../constants/app_constants.dart';
import '../storage/secure_storage_helper.dart';

class DioClient {
  static Dio? _instance;

  static Dio get instance {
    _instance ??= _createDio();
    return _instance!;
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
          final token = await SecureStorageHelper.getToken();
          if (token != null && token.isNotEmpty) {
            options.headers['Authorization'] = 'Bearer $token';
          }
          return handler.next(options);
        },
        onResponse: (response, handler) {
          return handler.next(response);
        },
        onError: (DioException error, handler) {
          // Xử lý thông điệp lỗi ProblemDetails từ ASP.NET Core
          String message = 'Đã có lỗi xảy ra. Vui lòng thử lại.';
          if (error.response?.data is Map) {
            final data = error.response!.data as Map;
            if (data.containsKey('detail') && data['detail'] != null) {
              message = data['detail'].toString();
            } else if (data.containsKey('title') && data['title'] != null) {
              message = data['title'].toString();
            } else if (data.containsKey('message') && data['message'] != null) {
              message = data['message'].toString();
            }
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
