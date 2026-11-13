import 'package:dio/dio.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../auth/auth_state.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class IdentityService {
  final Dio _dio = DioClient.instance;

  /// Khách lấy hồ sơ CCCD của chính mình
  Future<List<IdentityDocumentModel>> getMyDocuments() async {
    try {
      final response = await _dio.get(ApiEndpoints.identityDocumentsMine);
      if (response.data is List) {
        final list = response.data as List;
        return list.map((d) => IdentityDocumentModel.fromJson(d as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? CustomerMockData.identityDocuments : [];
    } catch (_) {
      return AuthState.isCurrentDemo ? CustomerMockData.identityDocuments : [];
    }
  }

  /// Manager lấy danh sách hồ sơ CCCD khách nộp chờ duyệt
  Future<List<ManagerIdentityReviewModel>> getManagerReviews({String? status}) async {
    try {
      final queryParams = <String, dynamic>{};
      if (status != null && status.isNotEmpty) queryParams['status'] = status;

      final response = await _dio.get(
        ApiEndpoints.identityDocuments,
        queryParameters: queryParams,
      );

      if (response.data is List) {
        final list = response.data as List;
        return list.map((d) => ManagerIdentityReviewModel.fromJson(d as Map<String, dynamic>)).toList();
      }
      return AuthState.isCurrentDemo ? ManagerMockData.identityReviews : [];
    } catch (_) {
      return AuthState.isCurrentDemo ? ManagerMockData.identityReviews : [];
    }
  }

  /// Khách nộp số CCCD để định danh
  Future<IdentityDocumentModel> submitDocument(String cccdNumber) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.identityDocuments,
        data: {'cccdNumber': cccdNumber.trim()},
      );

      final data = response.data as Map<String, dynamic>;
      final doc = IdentityDocumentModel.fromJson(data);
      CustomerMockData.identityDocuments.insert(0, doc);
      return doc;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Nộp hồ sơ CCCD thất bại.');
    } catch (_) {
      final newDoc = IdentityDocumentModel(
        id: DateTime.now().millisecondsSinceEpoch % 10000,
        fullName: 'Nguyễn Văn An',
        idNumber: cccdNumber,
        frontImageUrl: '',
        backImageUrl: '',
        status: 'Pending',
        uploadedAt: DateTime.now(),
      );
      CustomerMockData.identityDocuments.insert(0, newDoc);
      return newDoc;
    }
  }

  /// Quản lý duyệt hoặc từ chối hồ sơ CCCD (Approved hoặc Rejected)
  Future<void> reviewDocument(int id, String status) async {
    try {
      await _dio.put(
        ApiEndpoints.reviewIdentityDocument(id),
        data: {'status': status},
      );
      _updateLocalReview(id, status == 'Approved' ? 'Verified' : 'Rejected');
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Duyệt CCCD thất bại.');
    } catch (_) {
      _updateLocalReview(id, status == 'Approved' ? 'Verified' : 'Rejected');
    }
  }

  void _updateLocalReview(int id, String mappedStatus) {
    final idx = ManagerMockData.identityReviews.indexWhere((r) => r.id == id);
    if (idx != -1) {
      ManagerMockData.identityReviews[idx].status = mappedStatus;
    }
  }
}
