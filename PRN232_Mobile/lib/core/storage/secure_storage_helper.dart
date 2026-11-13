import 'dart:convert';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import '../constants/app_constants.dart';

class SecureStorageHelper {
  static const FlutterSecureStorage _storage = FlutterSecureStorage(
    aOptions: AndroidOptions(resetOnError: true),
  );

  static Future<void> saveToken(String token) async {
    try {
      await _storage.write(key: AppConstants.tokenKey, value: token);
    } catch (_) {}
  }

  static Future<String?> getToken() async {
    try {
      return await _storage.read(key: AppConstants.tokenKey);
    } catch (_) {
      return null;
    }
  }

  static Future<void> saveRole(String role) async {
    try {
      await _storage.write(key: AppConstants.roleKey, value: role);
    } catch (_) {}
  }

  static Future<String?> getRole() async {
    try {
      return await _storage.read(key: AppConstants.roleKey);
    } catch (_) {
      return null;
    }
  }

  static Future<void> saveUserData(Map<String, dynamic> userMap) async {
    try {
      await _storage.write(key: AppConstants.userKey, value: jsonEncode(userMap));
    } catch (_) {}
  }

  static Future<Map<String, dynamic>?> getUserData() async {
    try {
      final raw = await _storage.read(key: AppConstants.userKey);
      if (raw == null || raw.isEmpty) return null;
      return jsonDecode(raw) as Map<String, dynamic>;
    } catch (_) {
      return null;
    }
  }

  static Future<void> clearSession() async {
    try {
      await _storage.delete(key: AppConstants.tokenKey);
      await _storage.delete(key: AppConstants.userKey);
      await _storage.delete(key: AppConstants.roleKey);
    } catch (_) {}
  }
}
