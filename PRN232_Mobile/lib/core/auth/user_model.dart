import '../constants/app_constants.dart';

class UserModel {
  final int id;
  final String fullName;
  final String email;
  final String phoneNumber;
  final String role;
  final String? token;

  const UserModel({
    required this.id,
    required this.fullName,
    required this.email,
    required this.phoneNumber,
    required this.role,
    this.token,
  });

  bool get isCustomer => role.toLowerCase() == AppConstants.roleCustomer.toLowerCase();
  bool get isManager =>
      role.toLowerCase() == AppConstants.roleManager.toLowerCase() ||
      role.toLowerCase() == AppConstants.roleAdmin.toLowerCase();

  String get initials {
    final parts = fullName.trim().split(RegExp(r'\s+'));
    if (parts.isEmpty) return 'TV';
    if (parts.length == 1) return parts[0].substring(0, 1).toUpperCase();
    return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
  }

  factory UserModel.fromJson(Map<String, dynamic> json, {String? token}) {
    return UserModel(
      id: json['userId'] ?? json['id'] ?? 0,
      fullName: json['fullName'] ?? json['name'] ?? '',
      email: json['email'] ?? '',
      phoneNumber: json['phoneNumber'] ?? json['phone'] ?? '',
      role: json['role'] ?? AppConstants.roleCustomer,
      token: token ?? json['token'] ?? json['accessToken'],
    );
  }

  Map<String, dynamic> toJson() => {
        'id': id,
        'fullName': fullName,
        'email': email,
        'phoneNumber': phoneNumber,
        'role': role,
        if (token != null) 'token': token,
      };

  UserModel copyWith({
    String? fullName,
    String? email,
    String? phoneNumber,
    String? role,
    String? token,
  }) {
    return UserModel(
      id: id,
      fullName: fullName ?? this.fullName,
      email: email ?? this.email,
      phoneNumber: phoneNumber ?? this.phoneNumber,
      role: role ?? this.role,
      token: token ?? this.token,
    );
  }

  // Tài khoản Mock Demo phục vụ Test UI
  static const UserModel demoCustomer = UserModel(
    id: 101,
    fullName: 'Nguyễn Văn An',
    email: 'an.nguyen@example.com',
    phoneNumber: '0901234567',
    role: AppConstants.roleCustomer,
    token: 'mock_jwt_customer_token_123',
  );

  static const UserModel demoManager = UserModel(
    id: 202,
    fullName: 'Trần Thị Mai',
    email: 'mai.quanly@example.com',
    phoneNumber: '0912345678',
    role: AppConstants.roleManager,
    token: 'mock_jwt_manager_token_456',
  );
}
