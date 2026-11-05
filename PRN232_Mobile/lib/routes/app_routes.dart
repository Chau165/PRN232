class AppRoutes {
  AppRoutes._();

  // Root & Auth
  static const String splash = '/splash';
  static const String login = '/login';
  static const String register = '/register';

  // Common Profile & Notifications
  static const String profile = '/profile';
  static const String notifications = '/notifications';

  // Sub-screens
  static const String propertyDetail = '/property-detail';
  static const String roomDetail = '/room-detail';
  static const String deposit = '/deposit';
  static const String paymentSuccess = '/payment-success';

  // Customer Routes (Shell)
  static const String customerExplore = '/customer/explore';
  static const String customerMyRoom = '/customer/my-room';
  static const String customerBills = '/customer/bills';
  static const String customerProfile = '/customer/profile';

  // Manager Routes (Shell)
  static const String managerDashboard = '/manager/dashboard';
  static const String managerRooms = '/manager/rooms';
  static const String managerTasks = '/manager/tasks';
  static const String managerBilling = '/manager/billing';
  static const String managerProfile = '/manager/profile';

  // Default initial locations according to role
  static const String customerInitial = customerExplore;
  static const String managerInitial = managerDashboard;
}
