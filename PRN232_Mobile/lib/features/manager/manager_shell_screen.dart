import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../core/auth/auth_state.dart';
import '../../core/constants/app_colors.dart';
import '../../core/constants/app_typography.dart';
import '../../core/widgets/app_logo.dart';
import '../../routes/app_routes.dart';

class ManagerShellScreen extends StatelessWidget {
  final StatefulNavigationShell navigationShell;

  const ManagerShellScreen({
    super.key,
    required this.navigationShell,
  });

  void _onTap(int index) {
    navigationShell.goBranch(
      index,
      initialLocation: index == navigationShell.currentIndex,
    );
  }

  @override
  Widget build(BuildContext context) {
    return ListenableBuilder(
      listenable: authState,
      builder: (context, _) {
        final unreadCount = authState.unreadNotifications;

        return Scaffold(
          appBar: AppBar(
            title: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                const AppLogo(fontSize: 18, iconSize: 18),
                const SizedBox(width: 8),
                Container(
                  padding:
                      const EdgeInsets.symmetric(horizontal: 7, vertical: 2),
                  decoration: BoxDecoration(
                    color: AppColors.primary,
                    borderRadius: BorderRadius.circular(6),
                  ),
                  child: Text(
                    'QUẢN LÝ',
                    style: AppTypography.labelSmall.copyWith(
                      color: Colors.white,
                      fontSize: 10,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
              ],
            ),
            actions: [
              Stack(
                alignment: Alignment.center,
                children: [
                  IconButton(
                    icon: const Icon(Icons.notifications_outlined, size: 24),
                    onPressed: () => context.push(AppRoutes.notifications),
                  ),
                  if (unreadCount > 0)
                    Positioned(
                      top: 10,
                      right: 10,
                      child: Container(
                        padding: const EdgeInsets.all(4),
                        decoration: const BoxDecoration(
                          color: AppColors.danger,
                          shape: BoxShape.circle,
                        ),
                        constraints: const BoxConstraints(
                          minWidth: 16,
                          minHeight: 16,
                        ),
                        child: Text(
                          '$unreadCount',
                          style: const TextStyle(
                            color: Colors.white,
                            fontSize: 10,
                            fontWeight: FontWeight.bold,
                          ),
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ),
                ],
              ),
              const SizedBox(width: 8),
            ],
          ),
          body: navigationShell,
          bottomNavigationBar: Container(
            decoration: const BoxDecoration(
              border: Border(
                top: BorderSide(color: AppColors.borderLight, width: 1),
              ),
            ),
            child: NavigationBar(
              selectedIndex: navigationShell.currentIndex,
              onDestinationSelected: _onTap,
              destinations: const [
                NavigationDestination(
                  icon: Icon(Icons.dashboard_outlined),
                  selectedIcon: Icon(Icons.dashboard, color: AppColors.primary),
                  label: 'Tổng quan',
                ),
                NavigationDestination(
                  icon: Icon(Icons.meeting_room_outlined),
                  selectedIcon:
                      Icon(Icons.meeting_room, color: AppColors.primary),
                  label: 'Phòng & Khách',
                ),
                NavigationDestination(
                  icon: Icon(Icons.assignment_turned_in_outlined),
                  selectedIcon: Icon(Icons.assignment_turned_in,
                      color: AppColors.primary),
                  label: 'Duyệt việc',
                ),
                NavigationDestination(
                  icon: Icon(Icons.electric_meter_outlined),
                  selectedIcon:
                      Icon(Icons.electric_meter, color: AppColors.primary),
                  label: 'Thu chi',
                ),
                NavigationDestination(
                  icon: Icon(Icons.manage_accounts_outlined),
                  selectedIcon:
                      Icon(Icons.manage_accounts, color: AppColors.primary),
                  label: 'Tài khoản',
                ),
              ],
            ),
          ),
        );
      },
    );
  }
}
