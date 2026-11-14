/// Các hằng số toàn cục đồng bộ với Backend PRN232_BE (Application/States)
class AppConstants {
  AppConstants._();

  // Network & Deployment
  static const String appName = 'TrọViệt';
  static const String railwayApiUrl = 'https://prn232-production.up.railway.app';
  
  // Storage Keys
  static const String tokenKey = 'troviet_access_token';
  static const String userKey = 'troviet_current_user';
  static const String roleKey = 'troviet_current_role';

  // Role Names (Phù hợp với PRN232.Application.States.RoleNames)
  static const String roleCustomer = 'Customer';
  static const String roleManager = 'Manager';
  static const String roleAdmin = 'Admin';

  // Room Statuses (PRN232.Application.States.RoomStatuses)
  static const String roomAvailable = 'Available';
  static const String roomOccupied = 'Occupied';
  static const String roomMaintenance = 'Maintenance';
  static const String roomReserved = 'Reserved';

  // Booking Statuses (PRN232.Application.States.BookingStatuses)
  static const String bookingPending = 'Pending';
  static const String bookingDeposited = 'Deposited';
  static const String bookingCancelled = 'Cancelled';

  // Contract Statuses
  static const String contractActive = 'Active';

  // Invoice Statuses (PRN232.Application.States.InvoiceStatuses)
  static const String invoiceUnpaid = 'Unpaid';
  static const String invoicePaid = 'Paid';
  static const String invoiceOverdue = 'Overdue';

  // Maintenance Request Statuses (PRN232.Application.States.MaintenanceRequestStatuses)
  static const String maintenancePending = 'Pending';
  static const String maintenanceApproved = 'Approved';
  static const String maintenanceCompleted = 'Completed';

  // Viewing Statuses
  static const String viewingPending = 'Pending';
  static const String viewingApproved = 'Approved';
  static const String viewingRejected = 'Rejected';

  // Payment Methods
  static const String paymentVNPay = 'VNPay';
  static const String paymentMoMo = 'MoMo';
  static const String paymentCash = 'Cash';
}
