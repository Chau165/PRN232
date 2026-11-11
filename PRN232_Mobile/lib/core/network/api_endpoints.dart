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
  static String contractBillingTimeline(int id) => '/contracts/$id/billing-timeline';
  static String extendContract(int id) => '/contracts/$id/extend';
  static String terminateContract(int id) => '/contracts/$id/terminate';
  static String contractRoomReady(int id) => '/contracts/$id/room-ready';

  // Invoices & Complaints
  static const String invoices = '/invoices';
  static String invoiceDetail(int id) => '/invoices/$id';
  static const String previewInvoice = '/invoices/preview';
  static String payInvoice(int id) => '/invoices/$id/pay';
  static String recordCashInvoice(int id) => '/invoices/$id/record-cash';
  static const String invoiceComplaints = '/invoice-complaints';
  static String updateComplaint(int id) => '/invoice-complaints/$id';

  // Viewings
  static const String viewingSchedules = '/viewing-schedules';
  static String approveViewing(int id) => '/viewing-schedules/$id/approve';
  static String rejectViewing(int id) => '/viewing-schedules/$id/reject';

  // Maintenance & Issues
  static const String maintenanceRequests = '/maintenance-requests';
  static String maintenanceDetail(int id) => '/maintenance-requests/$id';
  static String maintenanceReports(int id) => '/maintenance-requests/$id/reports';
  static String maintenanceLogs(int id) => '/maintenance-requests/$id/logs';

  // Identity Documents (CCCD)
  static const String identityDocuments = '/identity-documents';
  static const String identityDocumentsMine = '/identity-documents/mine';
  static String reviewIdentityDocument(int id) => '/identity-documents/$id/review';

  // Equipments
  static const String equipments = '/equipments';
  static String equipmentDetail(int id) => '/equipments/$id';

  // Manager: Properties & Rooms
  static const String propertiesMyManaged = '/properties/my-managed';
  static const String rooms = '/rooms';
  static String roomDetail(int id) => '/rooms/$id';

  // Utility Readings
  static const String utilityReadings = '/utility-readings';

  // Notifications
  static const String notifications = '/notifications';
  static String updateNotification(int id) => '/notifications/$id';

  // User Profile
  static const String userMe = '/users/me';
  static const String passwordMe = '/users/me/password';
}
