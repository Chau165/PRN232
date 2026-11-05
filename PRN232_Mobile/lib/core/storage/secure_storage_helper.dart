import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../constants/app_constants.dart';

class SecureStorageHelper {
  static const FlutterSecureStorage _storage = FlutterSecureStorage();

  static Future<void> saveToken(String token) async {
    await _storage.write(key: AppConstants.tokenKey, value: token);
  }

  static Future<String?> getToken() async {
    return await _storage.read(key: AppConstants.tokenKey);
  }

  static Future<void> saveRole(String role) async {
    await _storage.write(key: AppConstants.roleKey, value: role);
  }

  static Future<String?> getRole() async {
    return await _storage.read(key: AppConstants.roleKey);
  }

  static Future<void> saveUserData(Map<String, dynamic> userMap) async {
    await _storage.write(key: AppConstants.userKey, value: jsonEncode(userMap));
  }

  static Future<Map<String, dynamic>?> getUserData() async {
    final raw = await _storage.read(key: AppConstants.userKey);
    if (raw == null || raw.isEmpty) return null;
    try {
      return jsonDecode(raw) as Map<String, dynamic>;
    } catch (_) {
      return null;
    }
  }

  static Future<void> clearSession() async {
    await _storage.delete(key: AppConstants.tokenKey);
    await _storage.delete(key: AppConstants.userKey);
    await _storage.delete(key: AppConstants.roleKey);
  }
}
