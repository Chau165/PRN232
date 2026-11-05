import 'package:flutter/foundation.dart';
import '../storage/secure_storage_helper.dart';
import 'user_model.dart';

class AuthState extends ChangeNotifier {
  static final AuthState instance = AuthState._();
  AuthState._() {
    _initSession();
  }

  UserModel? _currentUser;
  bool _isLoading = false;
  String? _errorMessage;
  int _unreadNotifications = 3;

  UserModel? get currentUser => _currentUser;
  bool get isAuthenticated => _currentUser != null;
  bool get isCustomer => _currentUser?.isCustomer ?? false;
  bool get isManager => _currentUser?.isManager ?? false;
  bool get isLoading => _isLoading;
  String? get errorMessage => _errorMessage;
  int get unreadNotifications => _unreadNotifications;

  Future<void> _initSession() async {
    final token = await SecureStorageHelper.getToken();
    final userMap = await SecureStorageHelper.getUserData();
    if (token != null && userMap != null) {
      _currentUser = UserModel.fromJson(userMap, token: token);
      notifyListeners();
    }
  }

  /// Đăng nhập bằng Email & Password (hỗ trợ cả demo lẫn API sau này)
  Future<bool> login(String email, String password) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    await Future.delayed(const Duration(milliseconds: 600));

    try {
      final trimmed = email.trim().toLowerCase();
      if (trimmed.contains('manager') || trimmed.contains('quanly')) {
        _currentUser = UserModel.demoManager.copyWith(
          email: trimmed,
        );
      } else {
        _currentUser = UserModel.demoCustomer.copyWith(
          email: trimmed,
        );
      }

      await SecureStorageHelper.saveToken(_currentUser!.token ?? 'mock_token');
      await SecureStorageHelper.saveRole(_currentUser!.role);
      await SecureStorageHelper.saveUserData(_currentUser!.toJson());

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _isLoading = false;
      _errorMessage = 'Đăng nhập không thành công. Vui lòng thử lại.';
      notifyListeners();
      return false;
    }
  }

  /// Chuyển đổi nhanh sang Role Customer để kiểm thử UI
  Future<void> loginAsCustomer() async {
    _currentUser = UserModel.demoCustomer;
    await SecureStorageHelper.saveToken(_currentUser!.token!);
    await SecureStorageHelper.saveRole(_currentUser!.role);
    await SecureStorageHelper.saveUserData(_currentUser!.toJson());
    notifyListeners();
  }

  /// Chuyển đổi nhanh sang Role Manager để kiểm thử UI
  Future<void> loginAsManager() async {
    _currentUser = UserModel.demoManager;
    await SecureStorageHelper.saveToken(_currentUser!.token!);
    await SecureStorageHelper.saveRole(_currentUser!.role);
    await SecureStorageHelper.saveUserData(_currentUser!.toJson());
    notifyListeners();
  }

  /// Đăng ký tài khoản
  Future<bool> register({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    await Future.delayed(const Duration(milliseconds: 700));

    try {
      _currentUser = UserModel(
        id: DateTime.now().millisecondsSinceEpoch % 10000,
        fullName: fullName.trim(),
        email: email.trim(),
        phoneNumber: phoneNumber.trim(),
        role: 'Customer',
        token: 'new_customer_jwt_${DateTime.now().millisecondsSinceEpoch}',
      );

      await SecureStorageHelper.saveToken(_currentUser!.token!);
      await SecureStorageHelper.saveRole(_currentUser!.role);
      await SecureStorageHelper.saveUserData(_currentUser!.toJson());

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _isLoading = false;
      _errorMessage = 'Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.';
      notifyListeners();
      return false;
    }
  }

  /// Cập nhật thông tin cá nhân
  Future<bool> updateProfile({required String fullName, required String phoneNumber}) async {
    if (_currentUser == null) return false;
    _isLoading = true;
    notifyListeners();

    await Future.delayed(const Duration(milliseconds: 400));
    _currentUser = _currentUser!.copyWith(
      fullName: fullName,
      phoneNumber: phoneNumber,
    );
    await SecureStorageHelper.saveUserData(_currentUser!.toJson());

    _isLoading = false;
    notifyListeners();
    return true;
  }

  /// Đăng xuất
  Future<void> logout() async {
    _currentUser = null;
    await SecureStorageHelper.clearSession();
    notifyListeners();
  }

  /// Đánh dấu đã đọc thông báo
  void markNotificationRead() {
    if (_unreadNotifications > 0) {
      _unreadNotifications--;
      notifyListeners();
    }
  }

  void markAllNotificationsRead() {
    _unreadNotifications = 0;
    notifyListeners();
  }
}

final authState = AuthState.instance;
