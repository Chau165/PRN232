class ApiEndpoints {
  ApiEndpoints._();

  // Auth
  static const String login = '/auth/login';
  static const String register = '/auth/register';

  // Public Properties & Rooms (No Auth Required)
  static const String publicProperties = '/public/properties';
  static const String publicPropertiesByArea = '/public/properties/by-area';
  static String publicPropertyDetail(int id) => '/public/properties/$id';
  static String publicPropertyRooms(int id) => '/public/properties/$id/rooms';
  static String publicRoomDetail(int roomId) => '/public/rooms/$roomId';

  // Customer: My Rentals & Contracts
  static const String myRentals = '/me/rentals';
  static const String myRentalsHistory = '/me/rentals/history';
  static const String bookings = '/bookings';
  static String bookingDetail(int id) => '/bookings/$id';
  static String payBooking(int id) => '/bookings/$id/pay';
  static String cancelBooking(int id) => '/bookings/$id/cancel';

  // Contracts
  static const String contracts = '/contracts';
  static String contractDetail(int id) => '/contracts/$id';

  // Invoices & Complaints
  static const String invoices = '/invoices';
  static String invoiceDetail(int id) => '/invoices/$id';
  static String payInvoice(int id) => '/invoices/$id/pay';
  static const String invoiceComplaints = '/invoice-complaints';

  // Viewings
  static const String viewingSchedules = '/viewing-schedules';
  static String approveViewing(int id) => '/viewing-schedules/$id/approve';
  static String rejectViewing(int id) => '/viewing-schedules/$id/reject';

  // Maintenance & Issues
  static const String maintenanceRequests = '/maintenance-requests';
  static String maintenanceDetail(int id) => '/maintenance-requests/$id';
  static String approveMaintenance(int id) => '/maintenance-requests/$id/approve';
  static String completeMaintenance(int id) => '/maintenance-requests/$id/complete';

  // Utility Readings
  static const String utilityReadings = '/utility-readings';

  // Notifications
  static const String notifications = '/notifications';
  static String markNotificationRead(int id) => '/notifications/$id/read';
  static const String markAllNotificationsRead = '/notifications/read-all';

  // User Profile
  static const String userMe = '/users/me';
}
