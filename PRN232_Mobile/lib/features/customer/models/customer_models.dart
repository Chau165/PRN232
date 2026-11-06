import 'package:latlong2/latlong.dart';

class AmenityItem {
  final int id;
  final String name;
  final String iconName;

  const AmenityItem({
    required this.id,
    required this.name,
    this.iconName = 'check',
  });
}

class LivingCostItem {
  final String serviceName;
  final num price;
  final String unit;
  final String basis; // usage, fixed_room, occupant, vehicle

  const LivingCostItem({
    required this.serviceName,
    required this.price,
    required this.unit,
    required this.basis,
  });
}

class RoomItem {
  final int roomId;
  final String roomCode;
  final String typeName;
  final num price;
  final num depositAmount;
  final double area;
  final int maxOccupancy;
  final String status;
  final List<String> images;
  final List<String> amenities;
  final String description;

  const RoomItem({
    required this.roomId,
    required this.roomCode,
    required this.typeName,
    required this.price,
    required this.depositAmount,
    required this.area,
    required this.maxOccupancy,
    required this.status,
    required this.images,
    required this.amenities,
    required this.description,
  });

  bool get isAvailable => status.toLowerCase() == 'available';
}

class PropertyItem {
  final int propertyId;
  final String propertyName;
  final String addressText;
  final String ward;
  final String district;
  final String city;
  final LatLng location;
  final String? primaryImageUrl;
  final List<String> galleryUrls;
  final int availableRoomCount;
  final num startingPrice;
  final String description;
  final List<AmenityItem> amenities;
  final List<LivingCostItem> livingCosts;
  final List<RoomItem> rooms;

  const PropertyItem({
    required this.propertyId,
    required this.propertyName,
    required this.addressText,
    required this.ward,
    required this.district,
    required this.city,
    required this.location,
    this.primaryImageUrl,
    required this.galleryUrls,
    required this.availableRoomCount,
    required this.startingPrice,
    required this.description,
    required this.amenities,
    required this.livingCosts,
    required this.rooms,
  });

  String get fullAddress => '$addressText, $ward, $district, $city';
}

class EquipmentItem {
  final int id;
  final String name;
  final String code;
  final String status; // Tốt, Cần sửa, Hỏng
  final String installedDate;

  const EquipmentItem({
    required this.id,
    required this.name,
    required this.code,
    required this.status,
    required this.installedDate,
  });
}

class InvoiceDetailLine {
  final String title;
  final String subtitle;
  final num amount;
  final num? quantity;
  final String? unit;

  const InvoiceDetailLine({
    required this.title,
    required this.subtitle,
    required this.amount,
    this.quantity,
    this.unit,
  });
}

class InvoiceModel {
  final int invoiceId;
  final String invoiceCode;
  final String monthYear;
  final DateTime dueDate;
  final num totalAmount;
  final String status; // Unpaid, Paid, Overdue
  final DateTime? paidDate;
  final String? paymentMethod;
  final List<InvoiceDetailLine> details;

  const InvoiceModel({
    required this.invoiceId,
    required this.invoiceCode,
    required this.monthYear,
    required this.dueDate,
    required this.totalAmount,
    required this.status,
    this.paidDate,
    this.paymentMethod,
    required this.details,
  });

  bool get isUnpaid => status == 'Unpaid';
  bool get isPaid => status == 'Paid';
}

// ==========================================
// BỔ SUNG CÁC MODEL FULL NGHIỆP VỤ CUSTOMER
// ==========================================

class CustomerViewingModel {
  final int id;
  final int propertyId;
  final String propertyName;
  final String propertyAddress;
  final int roomId;
  final String roomCode;
  final DateTime viewingDate;
  final String slotTime;
  final String? note;
  String status; // Pending, Confirmed, Cancelled, Completed

  CustomerViewingModel({
    required this.id,
    required this.propertyId,
    required this.propertyName,
    required this.propertyAddress,
    required this.roomId,
    required this.roomCode,
    required this.viewingDate,
    required this.slotTime,
    this.note,
    required this.status,
  });

  bool get isPending => status == 'Pending';
  bool get isConfirmed => status == 'Confirmed';
  bool get isCancelled => status == 'Cancelled';
}

class CustomerBookingModel {
  final int id;
  final String bookingCode;
  final int propertyId;
  final String propertyName;
  final String propertyAddress;
  final int roomId;
  final String roomCode;
  final String roomTypeName;
  final num depositAmount;
  final num monthlyPrice;
  final String paymentMethod;
  final DateTime bookingDate;
  final DateTime moveInDate;
  String status; // Pending, Confirmed, Cancelled, Expired, ContractCreated

  CustomerBookingModel({
    required this.id,
    required this.bookingCode,
    required this.propertyId,
    required this.propertyName,
    required this.propertyAddress,
    required this.roomId,
    required this.roomCode,
    required this.roomTypeName,
    required this.depositAmount,
    required this.monthlyPrice,
    required this.paymentMethod,
    required this.bookingDate,
    required this.moveInDate,
    required this.status,
  });

  bool get isPending => status == 'Pending';
  bool get isConfirmed => status == 'Confirmed';
  bool get isCancelled => status == 'Cancelled';
}

class ContractTimelineItem {
  final String periodName;
  final DateTime dueDate;
  final num amount;
  final bool isPaid;

  const ContractTimelineItem({
    required this.periodName,
    required this.dueDate,
    required this.amount,
    required this.isPaid,
  });
}

class CustomerContractModel {
  final int id;
  final String contractCode;
  final int propertyId;
  final String propertyName;
  final String propertyAddress;
  final int roomId;
  final String roomCode;
  final String roomTypeName;
  final String primaryTenantName;
  final String tenantCccd;
  final String tenantPhone;
  final DateTime startDate;
  DateTime endDate;
  num monthlyRent;
  final num depositAmount;
  final int paymentCycleMonths;
  final String terms;
  String status; // Active, ExpiringSoon, Terminated, Pending
  final List<ContractTimelineItem> billingTimeline;

  CustomerContractModel({
    required this.id,
    required this.contractCode,
    required this.propertyId,
    required this.propertyName,
    required this.propertyAddress,
    required this.roomId,
    required this.roomCode,
    required this.roomTypeName,
    required this.primaryTenantName,
    required this.tenantCccd,
    required this.tenantPhone,
    required this.startDate,
    required this.endDate,
    required this.monthlyRent,
    required this.depositAmount,
    this.paymentCycleMonths = 1,
    required this.terms,
    required this.status,
    required this.billingTimeline,
  });

  bool get isActive => status == 'Active' || status == 'ExpiringSoon';
  bool get isTerminated => status == 'Terminated';
}

class IdentityDocumentModel {
  final int id;
  final String fullName;
  final String idNumber;
  final DateTime? issueDate;
  final String frontImageUrl;
  final String backImageUrl;
  String status; // Pending, Verified, Rejected
  String? rejectReason;
  final DateTime uploadedAt;

  IdentityDocumentModel({
    required this.id,
    required this.fullName,
    required this.idNumber,
    this.issueDate,
    required this.frontImageUrl,
    required this.backImageUrl,
    required this.status,
    this.rejectReason,
    required this.uploadedAt,
  });

  bool get isPending => status == 'Pending';
  bool get isVerified => status == 'Verified';
  bool get isRejected => status == 'Rejected';
}

class CustomerMaintenanceModel {
  final int id;
  final String title;
  final String description;
  final String roomCode;
  final String propertyName;
  final String priority; // Normal, Urgent
  final List<String> images;
  String status; // Pending, InProgress, Completed, Cancelled
  final DateTime createdAt;
  String? technicianName;
  String? technicianPhone;
  num? actualCost;
  DateTime? completedDate;

  CustomerMaintenanceModel({
    required this.id,
    required this.title,
    required this.description,
    required this.roomCode,
    required this.propertyName,
    required this.priority,
    this.images = const [],
    required this.status,
    required this.createdAt,
    this.technicianName,
    this.technicianPhone,
    this.actualCost,
    this.completedDate,
  });

  bool get isPending => status == 'Pending';
  bool get isInProgress => status == 'InProgress';
  bool get isCompleted => status == 'Completed';
}
