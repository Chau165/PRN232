enum TaskType {
  viewing,
  deposit,
  maintenance,
}

enum TaskStatus {
  pending,
  approved,
  rejected,
  inProgress,
  completed,
}

class TenantModel {
  final int id;
  final String name;
  final String phone;
  final String cccd;
  final DateTime startDate;
  final bool isPrimary;
  final String? avatarUrl;

  const TenantModel({
    required this.id,
    required this.name,
    required this.phone,
    required this.cccd,
    required this.startDate,
    this.isPrimary = true,
    this.avatarUrl,
  });
}

class ManagerRoomModel {
  final int roomId;
  final String roomCode;
  final String typeName;
  final int floor;
  final double area;
  final num price;
  final num depositAmount;
  String status; // Occupied, Available, Reserved, Maintenance
  final List<TenantModel> tenants;
  final DateTime? contractEnd;

  ManagerRoomModel({
    required this.roomId,
    required this.roomCode,
    required this.typeName,
    required this.floor,
    required this.area,
    required this.price,
    required this.depositAmount,
    required this.status,
    required this.tenants,
    this.contractEnd,
  });

  bool get isAvailable => status == 'Available';
  bool get isOccupied => status == 'Occupied';
  bool get isReserved => status == 'Reserved';
  bool get isMaintenance => status == 'Maintenance';

  String get primaryTenantName =>
      tenants.isNotEmpty ? tenants.first.name : 'Chưa có người ở';

  String get primaryTenantPhone =>
      tenants.isNotEmpty ? tenants.first.phone : '';

  factory ManagerRoomModel.fromJson(Map<String, dynamic> json) {
    final rent = json['roomTypePrice'] ?? json['baseRent'] ?? json['price'] ?? 0;
    return ManagerRoomModel(
      roomId: json['roomId'] ?? json['id'] ?? 0,
      roomCode: json['roomCode'] ?? '',
      typeName: json['roomTypeName'] ?? 'Tiêu chuẩn',
      floor: 1,
      area: (json['areaSqm'] ?? 20).toDouble(),
      price: rent,
      depositAmount: json['depositAmount'] ?? rent,
      status: json['status'] ?? 'Available',
      tenants: const [],
      contractEnd: null,
    );
  }
}

class TechnicianModel {
  final int id;
  final String name;
  final String phone;
  final String specialty;
  final double rating;
  final bool isAvailable;

  const TechnicianModel({
    required this.id,
    required this.name,
    required this.phone,
    required this.specialty,
    required this.rating,
    this.isAvailable = true,
  });
}

class ManagerTaskModel {
  final int id;
  final TaskType type;
  TaskStatus status;
  final String title;
  final String description;
  final String customerName;
  final String customerPhone;
  final String roomCode;
  final String propertyName;
  final DateTime createdAt;
  final DateTime? targetDate;
  final num? amount;
  final String? paymentMethod;
  final String? priority; // Bình thường, Khẩn cấp
  String? technicianName;
  final List<String> evidenceImages;
  num? actualCost;
  String? completionNotes;
  String? invoiceReceiptImage;

  ManagerTaskModel({
    required this.id,
    required this.type,
    required this.status,
    required this.title,
    required this.description,
    required this.customerName,
    required this.customerPhone,
    required this.roomCode,
    required this.propertyName,
    required this.createdAt,
    this.targetDate,
    this.amount,
    this.paymentMethod,
    this.priority,
    this.technicianName,
    this.evidenceImages = const [],
    this.actualCost,
    this.completionNotes,
    this.invoiceReceiptImage,
  });

  bool get isPending => status == TaskStatus.pending;
  bool get isApproved => status == TaskStatus.approved;
  bool get isRejected => status == TaskStatus.rejected;
  bool get isInProgress => status == TaskStatus.inProgress;
  bool get isCompleted => status == TaskStatus.completed;

  factory ManagerTaskModel.fromViewingJson(Map<String, dynamic> json) {
    final st = json['status']?.toString();
    TaskStatus taskSt = TaskStatus.pending;
    if (st == 'Approved') taskSt = TaskStatus.approved;
    if (st == 'Rejected') taskSt = TaskStatus.rejected;

    final date = DateTime.tryParse(json['scheduledAt']?.toString() ?? '');

    return ManagerTaskModel(
      id: json['viewingId'] ?? json['id'] ?? 0,
      type: TaskType.viewing,
      status: taskSt,
      title: 'Hẹn xem phòng ${json['roomCode'] ?? ''}',
      description: json['managerNote'] ?? 'Khách hẹn xem phòng trực tiếp',
      customerName: json['customerName'] ?? 'Khách thuê',
      customerPhone: json['customerPhone'] ?? '',
      roomCode: json['roomCode'] ?? '',
      propertyName: 'Khu trọ',
      createdAt: date ?? DateTime.now(),
      targetDate: date,
    );
  }

  factory ManagerTaskModel.fromBookingJson(Map<String, dynamic> json) {
    final st = json['status']?.toString();
    TaskStatus taskSt = TaskStatus.pending;
    if (st == 'Deposited') taskSt = TaskStatus.approved;
    if (st == 'Cancelled') taskSt = TaskStatus.rejected;

    return ManagerTaskModel(
      id: json['bookingId'] ?? json['id'] ?? 0,
      type: TaskType.deposit,
      status: taskSt,
      title: 'Đơn đặt cọc phòng ${json['roomCode'] ?? ''}',
      description: 'Giữ chỗ phòng ${json['roomCode'] ?? ''}',
      customerName: json['customerName'] ?? 'Khách thuê',
      customerPhone: json['customerPhone'] ?? '',
      roomCode: json['roomCode'] ?? '',
      propertyName: json['propertyName'] ?? 'Khu trọ',
      createdAt: DateTime.now(),
      amount: json['depositAmount'] ?? 0,
      paymentMethod: 'Cổng trực tuyến',
    );
  }

  factory ManagerTaskModel.fromMaintenanceJson(Map<String, dynamic> json) {
    final st = json['status']?.toString();
    TaskStatus taskSt = TaskStatus.pending;
    if (st == 'Approved') taskSt = TaskStatus.inProgress;
    if (st == 'Completed') taskSt = TaskStatus.completed;

    return ManagerTaskModel(
      id: json['requestId'] ?? 0,
      type: TaskType.maintenance,
      status: taskSt,
      title: 'Báo hỏng phòng ${json['roomCode'] ?? ''}',
      description: json['description'] ?? '',
      customerName: json['customerName'] ?? 'Khách thuê',
      customerPhone: '',
      roomCode: json['roomCode'] ?? '',
      propertyName: 'Khu trọ',
      createdAt: DateTime.now(),
      priority: 'Bình thường',
      technicianName: json['assignedStaffName'],
    );
  }
}

class MeterReadingModel {
  final int roomId;
  final String roomCode;
  final String floor;
  final num oldElectricity;
  num? newElectricity;
  final num oldWater;
  num? newWater;
  DateTime? recordDate;
  bool isRecorded;
  String? meterImage;
  int? contractId;

  MeterReadingModel({
    required this.roomId,
    required this.roomCode,
    required this.floor,
    required this.oldElectricity,
    this.newElectricity,
    required this.oldWater,
    this.newWater,
    this.recordDate,
    this.isRecorded = false,
    this.meterImage,
    this.contractId,
  });

  num get electricityUsage =>
      (newElectricity != null && newElectricity! >= oldElectricity)
          ? (newElectricity! - oldElectricity)
          : 0;

  num get waterUsage =>
      (newWater != null && newWater! >= oldWater)
          ? (newWater! - oldWater)
          : 0;

  factory MeterReadingModel.fromJson(Map<String, dynamic> json) {
    return MeterReadingModel(
      roomId: json['roomId'] ?? 0,
      roomCode: json['roomCode'] ?? '',
      floor: 'Tầng 1',
      oldElectricity: json['electricStart'] ?? 0,
      newElectricity: json['electricEnd'],
      oldWater: json['waterStart'] ?? 0,
      newWater: json['waterEnd'],
      recordDate: json['readingMonth'] != null ? DateTime.tryParse(json['readingMonth'].toString()) : null,
      isRecorded: json['electricEnd'] != null,
      contractId: json['contractId'],
    );
  }
}

class ManagerInvoiceModel {
  final int invoiceId;
  final String invoiceCode;
  final String roomCode;
  final String tenantName;
  final num amount;
  String status; // Paid, Unpaid, Overdue
  final DateTime dueDate;
  final String monthYear;
  String? paymentMethod;

  ManagerInvoiceModel({
    required this.invoiceId,
    required this.invoiceCode,
    required this.roomCode,
    required this.tenantName,
    required this.amount,
    required this.status,
    required this.dueDate,
    required this.monthYear,
    this.paymentMethod,
  });

  bool get isPaid => status == 'Paid';
  bool get isUnpaid => status == 'Unpaid';
  bool get isOverdue => status == 'Overdue';

  factory ManagerInvoiceModel.fromJson(Map<String, dynamic> json) {
    final monthStr = json['invoiceMonth']?.toString() ?? '';
    final monthYearFormatted = monthStr.length >= 7
        ? 'Tháng ${monthStr.substring(5, 7)}/${monthStr.substring(0, 4)}'
        : 'Tháng hiện tại';

    return ManagerInvoiceModel(
      invoiceId: json['invoiceId'] ?? 0,
      invoiceCode: 'HD-${json['invoiceId'] ?? 0}',
      roomCode: json['roomCode'] ?? '',
      tenantName: json['customerName'] ?? 'Khách thuê',
      amount: json['netPayable'] ?? json['total'] ?? 0,
      status: json['status'] ?? 'Unpaid',
      dueDate: DateTime.tryParse(json['dueDate']?.toString() ?? '') ?? DateTime.now(),
      monthYear: monthYearFormatted,
      paymentMethod: json['paymentMethod']?.toString(),
    );
  }
}

// ==========================================
// BỔ SUNG CÁC MODEL FULL NGHIỆP VỤ MANAGER
// ==========================================

class ManagerContractModel {
  final int id;
  final String contractCode;
  final int propertyId;
  final String propertyName;
  final int roomId;
  final String roomCode;
  final String roomTypeName;
  final String primaryTenantName;
  final String tenantPhone;
  final String tenantCccd;
  final DateTime startDate;
  DateTime endDate;
  num monthlyRent;
  final num depositAmount;
  String status; // Active, ExpiringSoon, Terminated, Pending
  String? notes;

  ManagerContractModel({
    required this.id,
    required this.contractCode,
    required this.propertyId,
    required this.propertyName,
    required this.roomId,
    required this.roomCode,
    required this.roomTypeName,
    required this.primaryTenantName,
    required this.tenantPhone,
    required this.tenantCccd,
    required this.startDate,
    required this.endDate,
    required this.monthlyRent,
    required this.depositAmount,
    required this.status,
    this.notes,
  });

  bool get isActive => status == 'Active';
  bool get isExpiringSoon => status == 'ExpiringSoon';
  bool get isTerminated => status == 'Terminated';

  factory ManagerContractModel.fromJson(Map<String, dynamic> json) {
    return ManagerContractModel(
      id: json['contractId'] ?? 0,
      contractCode: 'HD-${json['contractId'] ?? 0}',
      propertyId: json['propertyId'] ?? 0,
      propertyName: json['propertyName'] ?? 'Nhà trọ',
      roomId: json['roomId'] ?? 0,
      roomCode: json['roomCode'] ?? '',
      roomTypeName: 'Tiêu chuẩn',
      primaryTenantName: json['customerName'] ?? 'Khách thuê',
      tenantPhone: json['customerPhone'] ?? '',
      tenantCccd: '',
      startDate: DateTime.tryParse(json['startDate']?.toString() ?? '') ?? DateTime.now(),
      endDate: DateTime.tryParse(json['endDate']?.toString() ?? '') ?? DateTime.now().add(const Duration(days: 365)),
      monthlyRent: json['monthlyRent'] ?? 0,
      depositAmount: json['depositAmount'] ?? 0,
      status: json['status'] ?? 'Active',
      notes: null,
    );
  }
}

class ManagerEquipmentModel {
  final int id;
  final String code;
  final String name;
  final String roomCode;
  final String category;
  String status; // Tốt, Cần sửa, Hỏng
  final String installedDate;
  final num? price;

  ManagerEquipmentModel({
    required this.id,
    required this.code,
    required this.name,
    required this.roomCode,
    required this.category,
    required this.status,
    required this.installedDate,
    this.price,
  });

  factory ManagerEquipmentModel.fromJson(Map<String, dynamic> json) {
    return ManagerEquipmentModel(
      id: json['equipmentId'] ?? 0,
      code: 'TB-${json['equipmentId'] ?? 0}',
      name: json['name'] ?? '',
      roomCode: json['roomCode'] ?? '',
      category: 'Gia dụng',
      status: json['status'] ?? 'Tốt',
      installedDate: json['installDate']?.toString() ?? '',
      price: null,
    );
  }
}

class ManagerIdentityReviewModel {
  final int id;
  final String tenantName;
  final String tenantPhone;
  final String roomCode;
  final String idNumber;
  final String frontImageUrl;
  final String backImageUrl;
  String status; // Pending, Verified, Rejected
  String? rejectReason;
  final DateTime uploadedAt;

  ManagerIdentityReviewModel({
    required this.id,
    required this.tenantName,
    required this.tenantPhone,
    required this.roomCode,
    required this.idNumber,
    required this.frontImageUrl,
    required this.backImageUrl,
    required this.status,
    this.rejectReason,
    required this.uploadedAt,
  });

  bool get isPending => status == 'Pending';
  bool get isVerified => status == 'Verified';
  bool get isRejected => status == 'Rejected';

  factory ManagerIdentityReviewModel.fromJson(Map<String, dynamic> json) {
    final st = json['status']?.toString() ?? 'Pending';
    final mappedStatus = st == 'Approved' ? 'Verified' : st;

    return ManagerIdentityReviewModel(
      id: json['identityDocumentId'] ?? json['id'] ?? 0,
      tenantName: json['customerName'] ?? 'Khách thuê',
      tenantPhone: '',
      roomCode: '',
      idNumber: json['cccdNumber'] ?? '',
      frontImageUrl: '',
      backImageUrl: '',
      status: mappedStatus,
      rejectReason: null,
      uploadedAt: DateTime.now(),
    );
  }
}
