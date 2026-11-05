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
    if (user == null) return;

    final nameController = TextEditingController(text: user.fullName);
    final phoneController = TextEditingController(text: user.phoneNumber);

    AppBottomSheet.show(
      context: context,
      title: 'Chỉnh sửa thông tin',
      subtitle: 'Cập nhật tên và số điện thoại liên hệ',
      child: Column(
        children: [
          AppTextField(
            label: 'Họ và tên',
            controller: nameController,
            prefixIcon: const Icon(Icons.person_outline),
          ),
          const SizedBox(height: 14),
          AppTextField(
            label: 'Số điện thoại',
            controller: phoneController,
            keyboardType: TextInputType.phone,
            prefixIcon: const Icon(Icons.phone_outlined),
          ),
          const SizedBox(height: 20),
          AppButton(
            text: 'Lưu thay đổi',
            onPressed: () async {
              if (nameController.text.trim().isNotEmpty) {
                await authState.updateProfile(
                  fullName: nameController.text.trim(),
                  phoneNumber: phoneController.text.trim(),
                );
                if (context.mounted) Navigator.pop(context);
              }
            },
          ),
        ],
      ),
    );
  }

  void _showChangePasswordBottomSheet(BuildContext context) {
    final currentPassController = TextEditingController();
    final newPassController = TextEditingController();
    final confirmPassController = TextEditingController();

    AppBottomSheet.show(
      context: context,
      title: 'Đổi mật khẩu',
      subtitle: 'Mật khẩu mới tối thiểu 6 ký tự',
      child: Column(
        children: [
          AppTextField(
            label: 'Mật khẩu hiện tại',
            controller: currentPassController,
            isPassword: true,
          ),
          const SizedBox(height: 14),
          AppTextField(
            label: 'Mật khẩu mới',
            controller: newPassController,
            isPassword: true,
          ),
          const SizedBox(height: 14),
          AppTextField(
            label: 'Xác nhận mật khẩu mới',
            controller: confirmPassController,
            isPassword: true,
          ),
          const SizedBox(height: 20),
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
          body: SingleChildScrollView(
            padding: const EdgeInsets.all(20),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                // Profile Avatar & Info Card
                AppCard(
                  padding: const EdgeInsets.all(20),
                  child: Row(
                    children: [
                      Container(
                        width: 64,
                        height: 64,
                        decoration: BoxDecoration(
                          color: AppColors.primary,
                          borderRadius: BorderRadius.circular(20),
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
                const SizedBox(height: 20),

                // Quick Switch Role (Tester Tool)
                AppCard(
                  color: AppColors.primaryLight,
                  border: const BorderSide(color: Color(0xFFC7E7E2)),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Row(
                        children: [
                          const Icon(Icons.swap_horiz_rounded,
                              color: AppColors.primary),
                          const SizedBox(width: 8),
                          Text('Chuyển đổi Role Demo',
                              style: AppTypography.headingSmall
                                  .copyWith(color: AppColors.primaryDark)),
                        ],
                      ),
                      const SizedBox(height: 6),
                      Text(
                        'Bạn đang ở chế độ: ${user.isManager ? "QUẢN LÝ (MANAGER)" : "KHÁCH THUÊ (CUSTOMER)"}. Nhấn nút dưới đây để hoán đổi nhanh:',
                        style: AppTypography.bodySmall
                            .copyWith(color: AppColors.primaryDark),
                      ),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          Expanded(
                            child: AppButton(
                              text: 'Sang Khách Thuê',
                              height: 40,
                              variant: user.isCustomer
                                  ? AppButtonVariant.primary
                                  : AppButtonVariant.outline,
                              onPressed: user.isCustomer
                                  ? null
                                  : () async {
                                      await authState.loginAsCustomer();
                                      if (context.mounted) {
                                        context.go(AppRoutes.customerInitial);
                                      }
                                    },
                            ),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: AppButton(
                              text: 'Sang Quản Lý',
                              height: 40,
                              variant: user.isManager
                                  ? AppButtonVariant.primary
                                  : AppButtonVariant.outline,
                              onPressed: user.isManager
                                  ? null
                                  : () async {
                                      await authState.loginAsManager();
                                      if (context.mounted) {
                                        context.go(AppRoutes.managerInitial);
                                      }
                                    },
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                ),
                const SizedBox(height: 20),

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
                const SizedBox(height: 24),

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
