import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../core/auth/auth_state.dart';
import '../features/auth/login_screen.dart';
import '../features/auth/register_screen.dart';
import '../features/auth/splash_screen.dart';
import '../features/customer/customer_shell_screen.dart';
import '../features/customer/data/customer_mock_data.dart';
import '../features/customer/models/customer_models.dart';
import '../features/customer/screens/customer_bills_screen.dart';
import '../features/customer/screens/customer_explore_screen.dart';
import '../features/customer/screens/customer_my_room_screen.dart';
import '../features/customer/screens/deposit_screen.dart';
import '../features/customer/screens/identity_documents_screen.dart';
import '../features/customer/screens/my_bookings_screen.dart';
import '../features/customer/screens/my_contracts_screen.dart';
import '../features/customer/screens/my_maintenance_requests_screen.dart';
import '../features/customer/screens/payment_success_screen.dart';
import '../features/customer/screens/property_detail_screen.dart';
import '../features/customer/screens/room_detail_screen.dart';
import '../features/manager/manager_shell_screen.dart';
import '../features/manager/screens/create_contract_screen.dart';
import '../features/manager/screens/manager_billing_screen.dart';
import '../features/manager/screens/manager_contracts_screen.dart';
import '../features/manager/screens/manager_dashboard_screen.dart';
import '../features/manager/screens/manager_equipment_screen.dart';
import '../features/manager/screens/manager_identity_review_screen.dart';
import '../features/manager/screens/manager_rooms_screen.dart';
import '../features/manager/screens/manager_tasks_screen.dart';
import '../features/profile/notifications_screen.dart';
import '../features/profile/profile_screen.dart';
import 'app_routes.dart';

final GlobalKey<NavigatorState> _rootNavigatorKey =
    GlobalKey<NavigatorState>(debugLabel: 'root');

final GoRouter appRouter = GoRouter(
  navigatorKey: _rootNavigatorKey,
  initialLocation: AppRoutes.splash,
  refreshListenable: authState,
  redirect: (BuildContext context, GoRouterState state) {
    final location = state.matchedLocation;
    final isAuth = authState.isAuthenticated;
    final isCustomer = authState.isCustomer;
    final isManager = authState.isManager;

    final isLoggingIn =
        location == AppRoutes.login || location == AppRoutes.register;
    final isSplash = location == AppRoutes.splash;

    if (isSplash) return null;

    // Chưa đăng nhập mà vào route bảo vệ -> về Login
    if (!isAuth) {
      return isLoggingIn ? null : AppRoutes.login;
    }

    // Đã đăng nhập mà cố vào login/register -> chuyển về trang chủ theo Role
    if (isLoggingIn) {
      return isManager ? AppRoutes.managerInitial : AppRoutes.customerInitial;
    }

    // Role Guard: Khách không vào màn Quản lý, Quản lý không vào màn Khách
    if (location.startsWith('/manager') && !isManager) {
      return AppRoutes.customerInitial;
    }
    if (location.startsWith('/customer') && !isCustomer) {
      return AppRoutes.managerInitial;
    }

    return null;
  },
  routes: [
    // Splash
    GoRoute(
      path: AppRoutes.splash,
      builder: (context, state) => const SplashScreen(),
    ),

    // Auth
    GoRoute(
      path: AppRoutes.login,
      builder: (context, state) => const LoginScreen(),
    ),
    GoRoute(
      path: AppRoutes.register,
      builder: (context, state) => const RegisterScreen(),
    ),

    // Notifications
    GoRoute(
      path: AppRoutes.notifications,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const NotificationsScreen(),
    ),

    // Full-screen customer details
    GoRoute(
      path: AppRoutes.propertyDetail,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) {
        final property = state.extra is PropertyItem
            ? state.extra as PropertyItem
            : CustomerMockData.properties.first;
        return PropertyDetailScreen(property: property);
      },
    ),
    GoRoute(
      path: AppRoutes.roomDetail,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) {
        final extraMap = state.extra as Map<String, dynamic>?;
        final property = extraMap?['property'] as PropertyItem? ??
            CustomerMockData.properties.first;
        final room = extraMap?['room'] as RoomItem? ?? property.rooms.first;
        return RoomDetailScreen(property: property, room: room);
      },
    ),
    GoRoute(
      path: AppRoutes.deposit,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) {
        final extraMap = state.extra as Map<String, dynamic>?;
        final property = extraMap?['property'] as PropertyItem? ??
            CustomerMockData.properties.first;
        final room = extraMap?['room'] as RoomItem? ?? property.rooms.first;
        return DepositScreen(property: property, room: room);
      },
    ),
    GoRoute(
      path: AppRoutes.paymentSuccess,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) {
        final extraMap = state.extra as Map<String, dynamic>? ?? {};
        return PaymentSuccessScreen(
          title: extraMap['title'] as String? ?? 'Thanh toán thành công',
          subtitle: extraMap['subtitle'] as String? ?? 'Giao dịch đã được ghi nhận',
          amount: extraMap['amount'] as num? ?? 0,
          method: extraMap['method'] as String? ?? 'VNPay',
          code: extraMap['code'] as String? ?? 'TXN-000000',
        );
      },
    ),

    // Customer Extended Sub-screens
    GoRoute(
      path: AppRoutes.customerBookings,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const MyBookingsScreen(),
    ),
    GoRoute(
      path: AppRoutes.customerContracts,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const MyContractsScreen(),
    ),
    GoRoute(
      path: AppRoutes.customerIdentity,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const IdentityDocumentsScreen(),
    ),
    GoRoute(
      path: AppRoutes.customerMaintenance,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const MyMaintenanceRequestsScreen(),
    ),

    // Manager Extended Sub-screens
    GoRoute(
      path: AppRoutes.managerContracts,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const ManagerContractsScreen(),
    ),
    GoRoute(
      path: AppRoutes.managerCreateContract,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const CreateContractScreen(),
    ),
    GoRoute(
      path: AppRoutes.managerEquipments,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const ManagerEquipmentScreen(),
    ),
    GoRoute(
      path: AppRoutes.managerIdentityReviews,
      parentNavigatorKey: _rootNavigatorKey,
      builder: (context, state) => const ManagerIdentityReviewScreen(),
    ),

    // ==========================================
    // CUSTOMER STATEFUL SHELL ROUTE (4 Tabs)
    // ==========================================
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) {
        return CustomerShellScreen(navigationShell: navigationShell);
      },
      branches: [
        // Tab 0: Khám phá / Tìm phòng
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.customerExplore,
              builder: (context, state) => const CustomerExploreScreen(),
            ),
          ],
        ),

        // Tab 1: Phòng của tôi
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.customerMyRoom,
              builder: (context, state) => const CustomerMyRoomScreen(),
            ),
          ],
        ),

        // Tab 2: Hóa đơn
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.customerBills,
              builder: (context, state) => const CustomerBillsScreen(),
            ),
          ],
        ),

        // Tab 3: Tài khoản
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.customerProfile,
              builder: (context, state) => const ProfileScreen(),
            ),
          ],
        ),
      ],
    ),

    // ==========================================
    // MANAGER STATEFUL SHELL ROUTE (5 Tabs)
    // ==========================================
    StatefulShellRoute.indexedStack(
      builder: (context, state, navigationShell) {
        return ManagerShellScreen(navigationShell: navigationShell);
      },
      branches: [
        // Tab 0: Tổng quan
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.managerDashboard,
              builder: (context, state) => const ManagerDashboardScreen(),
            ),
          ],
        ),

        // Tab 1: Phòng & Khách
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.managerRooms,
              builder: (context, state) => const ManagerRoomsScreen(),
            ),
          ],
        ),

        // Tab 2: Duyệt việc (Cọc, Xem phòng, Sửa chữa)
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.managerTasks,
              builder: (context, state) => const ManagerTasksScreen(),
            ),
          ],
        ),

        // Tab 3: Thu chi & Điện nước
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.managerBilling,
              builder: (context, state) => const ManagerBillingScreen(),
            ),
          ],
        ),

        // Tab 4: Tài khoản
        StatefulShellBranch(
          routes: [
            GoRoute(
              path: AppRoutes.managerProfile,
              builder: (context, state) => const ProfileScreen(),
            ),
          ],
        ),
      ],
    ),
  ],
);
