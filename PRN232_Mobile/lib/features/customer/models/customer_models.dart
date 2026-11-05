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
