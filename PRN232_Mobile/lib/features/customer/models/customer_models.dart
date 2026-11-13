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

  factory AmenityItem.fromJson(Map<String, dynamic> json) {
    return AmenityItem(
      id: json['amenityId'] ?? json['id'] ?? 0,
      name: json['name'] ?? '',
      iconName: 'check',
    );
  }
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

  factory LivingCostItem.fromJson(Map<String, dynamic> json) {
    return LivingCostItem(
      serviceName: json['serviceName'] ?? json['name'] ?? '',
      price: json['price'] ?? 0,
      unit: json['unitCode'] ?? json['unit'] ?? '',
      basis: json['billingBasis'] ?? json['basis'] ?? 'usage',
    );
  }
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

  factory RoomItem.fromJson(Map<String, dynamic> json) {
    final imagesList = (json['images'] as List?)?.map((e) => e.toString()).toList() ??
        (json['primaryImageUrl'] != null ? [json['primaryImageUrl'].toString()] : <String>[]);
    final amenitiesList = (json['amenities'] as List?)
            ?.map((e) => e is Map ? (e['name'] ?? '').toString() : e.toString())
            .toList() ??
        (json['amenitiesSummary'] as List?)
            ?.map((e) => e is Map ? (e['name'] ?? '').toString() : e.toString())
            .toList() ??
        <String>[];

    final type = json['roomTypeName'] ??
        (json['roomType'] is Map ? json['roomType']['roomTypeName'] : null) ??
        'Tiêu chuẩn';

    final rent = json['baseRent'] ?? json['roomTypePrice'] ?? json['price'] ?? 0;

    return RoomItem(
      roomId: json['roomId'] ?? json['id'] ?? 0,
      roomCode: json['roomCode'] ?? '',
      typeName: type.toString(),
      price: rent,
      depositAmount: json['depositAmount'] ?? rent,
      area: (json['areaSqm'] ?? json['area'] ?? 20).toDouble(),
      maxOccupancy: json['maxOccupancy'] ?? 2,
      status: json['status'] ?? 'Available',
      images: imagesList,
      amenities: amenitiesList,
      description: json['description'] ?? 'Phòng trọ tiện nghi, thoáng mát.',
    );
  }
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

  factory PropertyItem.fromJson(Map<String, dynamic> json) {
    final lat = (json['latitude'] != null ? num.tryParse(json['latitude'].toString()) : null)?.toDouble() ?? 10.7769;
    final lng = (json['longitude'] != null ? num.tryParse(json['longitude'].toString()) : null)?.toDouble() ?? 106.7009;

    final primaryImg = json['primaryImageUrl']?.toString();
    final amenitiesList = (json['commonAmenities'] as List?)?.map((a) => AmenityItem.fromJson(a as Map<String, dynamic>)).toList() ??
        (json['amenitiesSummary'] as List?)?.map((a) => AmenityItem.fromJson(a as Map<String, dynamic>)).toList() ??
        <AmenityItem>[];

    final livingCostsList = (json['livingCosts'] as List?)?.map((c) => LivingCostItem.fromJson(c as Map<String, dynamic>)).toList() ??
        <LivingCostItem>[];

    final roomsList = (json['rooms'] as List?)?.map((r) => RoomItem.fromJson(r as Map<String, dynamic>)).toList() ??
        <RoomItem>[];

    final propName = json['propertyName']?.toString() ?? 'Khu trọ';
    final addr = json['addressText']?.toString() ?? '';
    final w = json['ward']?.toString() ?? '';
    final d = json['district']?.toString() ?? '';
    final c = json['city']?.toString() ?? '';
    final fullAddr = [addr, w, d, c].where((s) => s.isNotEmpty).join(', ');

    final rawDesc = json['description']?.toString().trim();
    final dynamicDesc = (rawDesc != null && rawDesc.isNotEmpty)
        ? rawDesc
        : '$propName tọa lạc tại $fullAddr. Tòa nhà có không gian sống văn minh, an ninh 24/7, giờ giấc tự do, gần trường học, bệnh viện và các tiện ích sinh hoạt thuận tiện.';

    return PropertyItem(
      propertyId: json['propertyId'] ?? 0,
      propertyName: propName,
      addressText: addr,
      ward: w,
      district: d,
      city: c,
      location: LatLng(lat, lng),
      primaryImageUrl: primaryImg,
      galleryUrls: primaryImg != null ? [primaryImg] : const [],
      availableRoomCount: json['availableRoomCount'] ?? 0,
      startingPrice: json['startingPrice'] ?? 0,
      description: dynamicDesc,
      amenities: amenitiesList,
      livingCosts: livingCostsList,
      rooms: roomsList,
    );
  }
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

  factory EquipmentItem.fromJson(Map<String, dynamic> json) {
    return EquipmentItem(
      id: json['equipmentId'] ?? json['id'] ?? 0,
      name: json['name'] ?? '',
      code: 'TB-${json['equipmentId'] ?? json['id'] ?? 0}',
      status: json['status'] ?? 'Tốt',
      installedDate: json['installDate']?.toString() ?? '',
    );
  }
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

  factory InvoiceDetailLine.fromJson(Map<String, dynamic> json) {
    return InvoiceDetailLine(
      title: json['description'] ?? json['title'] ?? json['itemType'] ?? '',
      subtitle: json['itemType'] ?? '',
      amount: json['amount'] ?? 0,
      quantity: json['quantity'] != null ? num.tryParse(json['quantity'].toString()) : null,
      unit: json['unitCode']?.toString(),
    );
  }
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

  factory InvoiceModel.fromJson(Map<String, dynamic> json) {
    final lines = (json['lines'] as List?)
            ?.map((l) => InvoiceDetailLine.fromJson(l as Map<String, dynamic>))
            .toList() ??
        <InvoiceDetailLine>[];

    final monthStr = json['invoiceMonth']?.toString() ?? '';
    final monthYearFormatted = monthStr.length >= 7
        ? 'Tháng ${monthStr.substring(5, 7)}/${monthStr.substring(0, 4)}'
        : 'Tháng hiện tại';

    final due = DateTime.tryParse(json['dueDate']?.toString() ?? '') ?? DateTime.now();
    final paid = json['paymentDateUtc'] != null
        ? DateTime.tryParse(json['paymentDateUtc'].toString())
        : null;

    return InvoiceModel(
      invoiceId: json['invoiceId'] ?? 0,
      invoiceCode: 'HD-${json['invoiceId'] ?? 0}',
      monthYear: monthYearFormatted,
      dueDate: due,
      totalAmount: json['netPayable'] ?? json['total'] ?? 0,
      status: json['status'] ?? 'Unpaid',
      paidDate: paid,
      paymentMethod: json['paymentMethod']?.toString(),
      details: lines,
    );
  }
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
  bool get isConfirmed => status == 'Confirmed' || status == 'Approved';
  bool get isCancelled => status == 'Cancelled' || status == 'Rejected';

  factory CustomerViewingModel.fromJson(Map<String, dynamic> json) {
    final date = DateTime.tryParse(json['scheduledAt']?.toString() ?? '') ?? DateTime.now();
    final timeStr = '${date.hour.toString().padLeft(2, '0')}:${date.minute.toString().padLeft(2, '0')}';

    return CustomerViewingModel(
      id: json['viewingId'] ?? json['id'] ?? 0,
      propertyId: json['propertyId'] ?? 0,
      propertyName: json['propertyName'] ?? 'Nhà trọ',
      propertyAddress: json['addressText'] ?? 'Phòng ${json['roomCode'] ?? ''}',
      roomId: json['roomId'] ?? 0,
      roomCode: json['roomCode'] ?? '',
      viewingDate: date,
      slotTime: timeStr,
      note: json['managerNote'] ?? json['note'],
      status: json['status'] ?? 'Pending',
    );
  }
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
  bool get isConfirmed => status == 'Confirmed' || status == 'Deposited';
  bool get isCancelled => status == 'Cancelled';

  factory CustomerBookingModel.fromJson(Map<String, dynamic> json) {
    final expiry = DateTime.tryParse(json['expiryDate']?.toString() ?? '') ??
        DateTime.now().add(const Duration(days: 7));

    return CustomerBookingModel(
      id: json['bookingId'] ?? json['id'] ?? 0,
      bookingCode: 'DC-${json['bookingId'] ?? 0}',
      propertyId: json['propertyId'] ?? 0,
      propertyName: json['propertyName'] ?? 'Nhà trọ',
      propertyAddress: json['propertyAddress'] ?? 'Phòng ${json['roomCode'] ?? ''}',
      roomId: json['roomId'] ?? 0,
      roomCode: json['roomCode'] ?? '',
      roomTypeName: 'Phòng tiêu chuẩn',
      depositAmount: json['depositAmount'] ?? 0,
      monthlyPrice: json['depositAmount'] ?? 0,
      paymentMethod: 'Cổng trực tuyến',
      bookingDate: DateTime.now(),
      moveInDate: expiry,
      status: json['status'] ?? 'Pending',
    );
  }
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

  factory ContractTimelineItem.fromJson(Map<String, dynamic> json) {
    return ContractTimelineItem(
      periodName: 'Kỳ ${json['periodIndex'] ?? 1}',
      dueDate: DateTime.tryParse(json['dueDate']?.toString() ?? '') ?? DateTime.now(),
      amount: json['expectedRent'] ?? 0,
      isPaid: json['status'] == 'Paid',
    );
  }
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

  factory CustomerContractModel.fromJson(Map<String, dynamic> json) {
    return CustomerContractModel(
      id: json['contractId'] ?? 0,
      contractCode: 'HD-${json['contractId'] ?? 0}',
      propertyId: json['propertyId'] ?? 0,
      propertyName: json['propertyName'] ?? 'Nhà trọ',
      propertyAddress: json['propertyAddress'] ?? '',
      roomId: json['roomId'] ?? 0,
      roomCode: json['roomCode'] ?? '',
      roomTypeName: 'Phòng tiêu chuẩn',
      primaryTenantName: json['customerName'] ?? '',
      tenantCccd: '',
      tenantPhone: json['customerPhone'] ?? '',
      startDate: DateTime.tryParse(json['startDate']?.toString() ?? '') ?? DateTime.now(),
      endDate: DateTime.tryParse(json['endDate']?.toString() ?? '') ?? DateTime.now().add(const Duration(days: 365)),
      monthlyRent: json['monthlyRent'] ?? 0,
      depositAmount: json['depositAmount'] ?? 0,
      paymentCycleMonths: 1,
      terms: 'Hợp đồng thuê phòng chính thức',
      status: json['status'] ?? 'Active',
      billingTimeline: const [],
    );
  }
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
  bool get isVerified => status == 'Verified' || status == 'Approved';
  bool get isRejected => status == 'Rejected';

  factory IdentityDocumentModel.fromJson(Map<String, dynamic> json) {
    final st = json['status']?.toString() ?? 'Pending';
    final mappedStatus = st == 'Approved' ? 'Verified' : st;

    return IdentityDocumentModel(
      id: json['identityDocumentId'] ?? json['id'] ?? 0,
      fullName: json['customerName'] ?? 'Khách thuê',
      idNumber: json['cccdNumber'] ?? '',
      issueDate: null,
      frontImageUrl: '',
      backImageUrl: '',
      status: mappedStatus,
      rejectReason: null,
      uploadedAt: DateTime.now(),
    );
  }
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
  bool get isInProgress => status == 'InProgress' || status == 'Approved';
  bool get isCompleted => status == 'Completed';

  factory CustomerMaintenanceModel.fromJson(Map<String, dynamic> json) {
    final st = json['status']?.toString() ?? 'Pending';
    final mappedStatus = st == 'Approved' ? 'InProgress' : st;

    return CustomerMaintenanceModel(
      id: json['requestId'] ?? 0,
      title: json['equipmentName'] != null
          ? 'Hỏng ${json['equipmentName']}'
          : 'Báo hỏng phòng ${json['roomCode'] ?? ''}',
      description: json['description'] ?? '',
      roomCode: json['roomCode'] ?? '',
      propertyName: 'Khu trọ',
      priority: 'Bình thường',
      images: const [],
      status: mappedStatus,
      createdAt: DateTime.now(),
      technicianName: json['assignedStaffName'],
      technicianPhone: null,
      actualCost: null,
      completedDate: null,
    );
  }
}
