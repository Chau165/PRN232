import 'package:dio/dio.dart';
import '../../features/profile/notifications_screen.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class NotificationService {
  final Dio _dio = DioClient.instance;

  Future<List<NotificationItem>> getNotifications({bool? isRead}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (isRead != null) queryParams['isRead'] = isRead;

      final response = await _dio.get(
        ApiEndpoints.notifications,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((n) {
            final map = n as Map<String, dynamic>;
            return NotificationItem(
              id: map['notificationId'] ?? map['id'] ?? 0,
              title: map['title'] ?? 'Thông báo mới',
              message: map['body'] ?? '',
              type: map['type'] ?? 'Notification',
              createdAt: DateTime.now(),
              isRead: map['isRead'] ?? false,
            );
          }).toList();
        }
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  Future<void> markAsRead(int notificationId) async {
    try {
      await _dio.patch(
        ApiEndpoints.updateNotification(notificationId),
        data: {'isRead': true},
      );
    } catch (_) {}
  }
}
