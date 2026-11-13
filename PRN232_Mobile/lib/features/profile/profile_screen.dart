import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../core/auth/auth_state.dart';
import '../../core/constants/app_colors.dart';
import '../../core/constants/app_typography.dart';
import '../../core/widgets/app_bottom_sheet.dart';
import '../../core/widgets/app_button.dart';
import '../../core/widgets/app_card.dart';
import '../../core/widgets/app_text_field.dart';
import '../../core/widgets/status_badge.dart';
import '../../routes/app_routes.dart';

class ProfileScreen extends StatelessWidget {
  const ProfileScreen({super.key});

  void _showEditProfileBottomSheet(BuildContext context) {
    final user = authState.currentUser;
    final nameController = TextEditingController(text: user?.fullName);
    final phoneController = TextEditingController(text: user?.phoneNumber);

    AppBottomSheet.show(
      context: context,
      title: 'Chỉnh sửa thông tin cá nhân',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          AppTextField(
            label: 'Họ và tên',
            controller: nameController,
          ),
          const SizedBox(height: 16),
          AppTextField(
            label: 'Số điện thoại',
            controller: phoneController,
            keyboardType: TextInputType.phone,
          ),
          const SizedBox(height: 24),
          AppButton(
            text: 'Lưu thay đổi',
            onPressed: () {
              if (nameController.text.trim().isNotEmpty) {
                authState.updateProfile(
                  fullName: nameController.text.trim(),
                  phoneNumber: phoneController.text.trim(),
                );
                Navigator.pop(context);
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(
                    content: Text('Đã cập nhật thông tin cá nhân thành công!'),
                  ),
                );
              }
            },
          ),
        ],
      ),
    );
  }

  void _showChangePasswordBottomSheet(BuildContext context) {
    final oldPasswordController = TextEditingController();
    final newPasswordController = TextEditingController();

    AppBottomSheet.show(
      context: context,
      title: 'Đổi mật khẩu',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          AppTextField(
            label: 'Mật khẩu hiện tại',
            isPassword: true,
            controller: oldPasswordController,
          ),
          const SizedBox(height: 16),
          AppTextField(
            label: 'Mật khẩu mới',
            isPassword: true,
            controller: newPasswordController,
          ),
          const SizedBox(height: 24),
          AppButton(
            text: 'Cập nhật mật khẩu',
            onPressed: () {
              Navigator.pop(context);
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Mật khẩu đã được thay đổi.')),
              );
            },
          ),
        ],
      ),
    );
  }

  void _confirmLogout(BuildContext context) {
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Đăng xuất'),
        content: const Text('Bạn có chắc chắn muốn đăng xuất khỏi tài khoản?'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Hủy'),
          ),
          TextButton(
            onPressed: () async {
              Navigator.pop(ctx);
              await authState.logout();
              if (context.mounted) context.go(AppRoutes.login);
            },
            child: const Text('Đăng xuất',
                style: TextStyle(color: AppColors.danger)),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: authState,
      builder: (context, _) {
        final user = authState.currentUser;
        if (user == null) {
          return Center(
            child: AppButton(
              text: 'Đăng nhập',
              width: 160,
              onPressed: () => context.go(AppRoutes.login),
            ),
          );
        }

        return Scaffold(
          backgroundColor: AppColors.background,
          appBar: AppBar(
            title: const Text('Tài Khoản & Hồ Sơ'),
            backgroundColor: Colors.white,
            elevation: 0,
          ),
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Profile Avatar & Info Card
                AppCard(
                  padding: const EdgeInsets.all(18),
                  child: Row(
                    children: [
                      Container(
                        width: 60,
                        height: 60,
                        decoration: BoxDecoration(
                          color: AppColors.primary,
                          borderRadius: BorderRadius.circular(18),
                        ),
                        alignment: Alignment.center,
                        child: Text(
                          user.initials,
                          style: AppTypography.displayMedium.copyWith(
                            color: Colors.white,
                            fontSize: 22,
                          ),
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Expanded(
                                  child: Text(
                                    user.fullName,
                                    style: AppTypography.headingLarge,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                  ),
                                ),
                                StatusBadge(
                                  label: user.isManager ? 'Quản lý' : 'Khách thuê',
                                  tone: user.isManager
                                      ? StatusTone.teal
                                      : StatusTone.green,
                                ),
                              ],
                            ),
                            const SizedBox(height: 4),
                            Text(user.email, style: AppTypography.bodySmall),
                            const SizedBox(height: 2),
                            Text(user.phoneNumber,
                                style: AppTypography.bodySmall),
                          ],
                        ),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 18),

                // Extended Business Features for Customer
                if (user.isCustomer) ...[
                  Text('Quản lý thuê trọ & Giấy tờ', style: AppTypography.headingMedium),
                  const SizedBox(height: 10),
                  AppCard(
                    padding: EdgeInsets.zero,
                    child: Column(
                      children: [
                        ListTile(
                          leading: const Icon(Icons.bookmark_border_rounded,
                              color: AppColors.primary),
                          title: Text('Lịch hẹn & Cọc phòng', style: AppTypography.labelLarge),
                          subtitle: Text('Theo dõi các cuộc hẹn xem phòng và đơn cọc', style: AppTypography.bodySmall),
                          trailing: const Icon(Icons.chevron_right, color: AppColors.textMuted),
                          onTap: () => context.push(AppRoutes.customerBookings),
                        ),
                        const Divider(height: 1),
                        ListTile(
                          leading: const Icon(Icons.description_outlined,
                              color: AppColors.primary),
                          title: Text('Hợp đồng thuê của tôi', style: AppTypography.labelLarge),
                          subtitle: Text('Xem điều khoản và timeline công nợ', style: AppTypography.bodySmall),
                          trailing: const Icon(Icons.chevron_right, color: AppColors.textMuted),
                          onTap: () => context.push(AppRoutes.customerContracts),
                        ),
                        const Divider(height: 1),
                        ListTile(
                          leading: const Icon(Icons.badge_outlined,
                              color: AppColors.primary),
                          title: Text('Hồ sơ định danh CCCD', style: AppTypography.labelLarge),
                          subtitle: Text('Đăng ký lưu trú và xác thực tài khoản', style: AppTypography.bodySmall),
                          trailing: const Icon(Icons.chevron_right, color: AppColors.textMuted),
                          onTap: () => context.push(AppRoutes.customerIdentity),
                        ),
                        const Divider(height: 1),
                        ListTile(
                          leading: const Icon(Icons.handyman_outlined,
                              color: AppColors.primary),
                          title: Text('Lịch sử báo hỏng & sửa chữa', style: AppTypography.labelLarge),
                          subtitle: Text('Tiến trình xử lý sự cố thiết bị trong phòng', style: AppTypography.bodySmall),
                          trailing: const Icon(Icons.chevron_right, color: AppColors.textMuted),
                          onTap: () => context.push(AppRoutes.customerMaintenance),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),
                ],

                // Extended Business Features for Manager
                if (user.isManager) ...[
                  Text('Nghiệp vụ quản lý mở rộng', style: AppTypography.headingMedium),
                  const SizedBox(height: 10),
                  AppCard(
                    padding: EdgeInsets.zero,
                    child: Column(
                      children: [
                        ListTile(
                          leading: const Icon(Icons.description_outlined,
                              color: AppColors.primary),
                          title: Text('Quản lý hợp đồng thuê', style: AppTypography.labelLarge),
                          subtitle: Text('Lập mới, gia hạn và thanh lý hợp đồng', style: AppTypography.bodySmall),
                          trailing: const Icon(Icons.chevron_right, color: AppColors.textMuted),
                          onTap: () => context.push(AppRoutes.managerContracts),
                        ),
                        const Divider(height: 1),
                        ListTile(
                          leading: const Icon(Icons.devices_other_rounded,
                              color: AppColors.primary),
                          title: Text('Kiểm kê tài sản & Thiết bị', style: AppTypography.labelLarge),
                          subtitle: Text('Quản lý trang thiết bị các phòng', style: AppTypography.bodySmall),
                          trailing: const Icon(Icons.chevron_right, color: AppColors.textMuted),
                          onTap: () => context.push(AppRoutes.managerEquipments),
                        ),
                        const Divider(height: 1),
                        ListTile(
                          leading: const Icon(Icons.verified_user_outlined,
                              color: AppColors.primary),
                          title: Text('Kiểm duyệt CCCD khách thuê', style: AppTypography.labelLarge),
                          subtitle: Text('Duyệt hồ sơ định danh và lưu trú', style: AppTypography.bodySmall),
                          trailing: const Icon(Icons.chevron_right, color: AppColors.textMuted),
                          onTap: () => context.push(AppRoutes.managerIdentityReviews),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 20),
                ],

                // Settings & Actions
                Text('Cài đặt tài khoản', style: AppTypography.headingMedium),
                const SizedBox(height: 10),
                AppCard(
                  padding: EdgeInsets.zero,
                  child: Column(
                    children: [
                      ListTile(
                        leading: const Icon(Icons.edit_outlined,
                            color: AppColors.textPrimary),
                        title: Text('Chỉnh sửa thông tin',
                            style: AppTypography.labelLarge),
                        trailing: const Icon(Icons.chevron_right,
                            color: AppColors.textMuted),
                        onTap: () => _showEditProfileBottomSheet(context),
                      ),
                      const Divider(height: 1),
                      ListTile(
                        leading: const Icon(Icons.lock_reset_rounded,
                            color: AppColors.textPrimary),
                        title: Text('Đổi mật khẩu',
                            style: AppTypography.labelLarge),
                        trailing: const Icon(Icons.chevron_right,
                            color: AppColors.textMuted),
                        onTap: () => _showChangePasswordBottomSheet(context),
                      ),
                      const Divider(height: 1),
                      ListTile(
                        leading: const Icon(Icons.notifications_none_rounded,
                            color: AppColors.textPrimary),
                        title: Text('Thông báo hệ thống',
                            style: AppTypography.labelLarge),
                        trailing: const Icon(Icons.chevron_right,
                            color: AppColors.textMuted),
                        onTap: () => context.push(AppRoutes.notifications),
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),

                // Logout Button
                AppButton(
                  text: 'Đăng xuất',
                  variant: AppButtonVariant.outline,
                  icon: const Icon(Icons.logout_rounded,
                      color: AppColors.primary, size: 20),
                  onPressed: () => _confirmLogout(context),
                ),
                const SizedBox(height: 30),
              ],
            ),
          ),
        );
      },
    );
  }
}
