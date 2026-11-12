import 'package:flutter/foundation.dart';
import '../services/auth_service.dart';
import '../storage/secure_storage_helper.dart';
import 'user_model.dart';

class AuthState extends ChangeNotifier {
  static final AuthState instance = AuthState._();
  AuthState._() {
    _initSession();
  }

  final AuthService _authService = AuthService();
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
      // Thử đồng bộ profile mới nhất từ server
      try {
        final me = await _authService.getMe();
        _currentUser = me.copyWith(token: token);
        await SecureStorageHelper.saveUserData(_currentUser!.toJson());
        notifyListeners();
      } catch (_) {
        // Giữ phiên offline nếu server chưa sẵn sàng
      }
    }
  }

  /// Đăng nhập bằng Email & Password (gọi Railway API thật)
  Future<bool> login(String email, String password) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final user = await _authService.login(email, password);
      _currentUser = user;

      await SecureStorageHelper.saveToken(user.token ?? '');
      await SecureStorageHelper.saveRole(user.role);
      await SecureStorageHelper.saveUserData(user.toJson());

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      // Fallback demo cho tài khoản kiểm thử UI
      final trimmed = email.trim().toLowerCase();
      if (trimmed == UserModel.demoCustomer.email ||
          trimmed == UserModel.demoManager.email ||
          trimmed.contains('demo') ||
          trimmed.contains('quanly')) {
        if (trimmed.contains('manager') || trimmed.contains('quanly')) {
          _currentUser = UserModel.demoManager.copyWith(email: trimmed);
        } else {
          _currentUser = UserModel.demoCustomer.copyWith(email: trimmed);
        }
        await SecureStorageHelper.saveToken(_currentUser!.token ?? 'mock_token');
        await SecureStorageHelper.saveRole(_currentUser!.role);
        await SecureStorageHelper.saveUserData(_currentUser!.toJson());

        _isLoading = false;
        notifyListeners();
        return true;
      }

      _isLoading = false;
      _errorMessage = e.toString().replaceFirst('Exception: ', '');
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

  /// Đăng ký tài khoản (gọi Railway API thật)
  Future<bool> register({
    required String fullName,
    required String email,
    required String phoneNumber,
    required String password,
  }) async {
    _isLoading = true;
    _errorMessage = null;
    notifyListeners();

    try {
      final user = await _authService.register(
        fullName: fullName,
        email: email,
        phoneNumber: phoneNumber,
        password: password,
      );
      _currentUser = user;

      await SecureStorageHelper.saveToken(user.token ?? '');
      await SecureStorageHelper.saveRole(user.role);
      await SecureStorageHelper.saveUserData(user.toJson());

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      _isLoading = false;
      _errorMessage = e.toString().replaceFirst('Exception: ', '');
      notifyListeners();
      return false;
    }
  }

  /// Cập nhật thông tin cá nhân
  Future<bool> updateProfile({
    required String fullName,
    required String phoneNumber,
  }) async {
    if (_currentUser == null) return false;
    _isLoading = true;
    notifyListeners();

    try {
      final updated = await _authService.updateProfile(
        fullName: fullName,
        phoneNumber: phoneNumber,
      );
      _currentUser = updated.copyWith(token: _currentUser!.token);
      await SecureStorageHelper.saveUserData(_currentUser!.toJson());

      _isLoading = false;
      notifyListeners();
      return true;
    } catch (e) {
      // Fallback local update
      _currentUser = _currentUser!.copyWith(
        fullName: fullName,
        phoneNumber: phoneNumber,
      );
      await SecureStorageHelper.saveUserData(_currentUser!.toJson());
      _isLoading = false;
      notifyListeners();
      return true;
    }
  }

  /// Đổi mật khẩu
  Future<bool> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      await _authService.changePassword(
        currentPassword: currentPassword,
        newPassword: newPassword,
      );
      return true;
    } catch (e) {
      _errorMessage = e.toString().replaceFirst('Exception: ', '');
      notifyListeners();
      return false;
    }
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
