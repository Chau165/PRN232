import 'package:latlong2/latlong.dart';
import '../models/customer_models.dart';

class CustomerMockData {
  CustomerMockData._();

  // Danh mục tiện ích chuẩn
  static const List<AmenityItem> defaultAmenities = [
    AmenityItem(id: 1, name: 'Khóa vân tay 24/7', iconName: 'fingerprint'),
    AmenityItem(id: 2, name: 'Máy giặt chung', iconName: 'local_laundry_service'),
    AmenityItem(id: 3, name: 'Nhà để xe rộng rãi', iconName: 'two_wheeler'),
    AmenityItem(id: 4, name: 'Camera an ninh', iconName: 'videocam'),
    AmenityItem(id: 5, name: 'Internet cáp quang', iconName: 'wifi'),
    AmenityItem(id: 6, name: 'Không chung chủ', iconName: 'key'),
    AmenityItem(id: 7, name: 'Thang máy', iconName: 'elevator'),
    AmenityItem(id: 8, name: 'Ban công thoáng mát', iconName: 'balcony'),
  ];

  // Danh mục chi phí dịch vụ sinh hoạt chuẩn
  static const List<LivingCostItem> defaultLivingCosts = [
    LivingCostItem(
      serviceName: 'Điện sinh hoạt',
      price: 3500,
      unit: 'kWh',
      basis: 'Theo chỉ số đồng hồ riêng',
    ),
    LivingCostItem(
      serviceName: 'Nước máy sinh hoạt',
      price: 20000,
      unit: 'm³',
      basis: 'Theo chỉ số đồng hồ riêng',
    ),
    LivingCostItem(
      serviceName: 'Internet tốc độ cao',
      price: 100000,
      unit: 'tháng/phòng',
      basis: 'Cố định hàng tháng',
    ),
    LivingCostItem(
      serviceName: 'Phí dịch vụ & rác',
      price: 50000,
      unit: 'tháng/người',
      basis: 'Vệ sinh hành lang & rác',
    ),
    LivingCostItem(
      serviceName: 'Giữ xe máy',
      price: 120000,
      unit: 'tháng/xe',
      basis: 'Xe đầu tiên miễn phí',
    ),
  ];

  // Danh sách các khu trọ
  static final List<PropertyItem> properties = [
    PropertyItem(
      propertyId: 1,
      propertyName: 'Nhà trọ Bình An',
      addressText: '12 Đường Số 5',
      ward: 'Phường 10',
      district: 'Gò Vấp',
      city: 'TP. Hồ Chí Minh',
      location: const LatLng(10.8385, 106.6652),
      primaryImageUrl:
          'https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=800&auto=format&fit=crop&q=80',
      galleryUrls: [
        'https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=800&auto=format&fit=crop&q=80',
        'https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=800&auto=format&fit=crop&q=80',
        'https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?w=800&auto=format&fit=crop&q=80',
      ],
      availableRoomCount: 3,
      startingPrice: 3200000,
      description:
          'Tòa nhà trọ mới xây gồm 4 tầng, môi trường văn minh yên tĩnh, phù hợp sinh viên và người đi làm. Gần chợ, trạm xe buýt và trường Đại học Công nghiệp TP.HCM (IUH). Giờ giấc tự do, ra vào bằng vân tay.',
      amenities: defaultAmenities,
      livingCosts: defaultLivingCosts,
      rooms: [
        const RoomItem(
          roomId: 101,
          roomCode: 'P.101',
          typeName: 'Studio Ban Công Thoáng Mát',
          price: 3500000,
          depositAmount: 3500000,
          area: 28.0,
          maxOccupancy: 2,
          status: 'Available',
          images: [
            'https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?w=800&auto=format&fit=crop&q=80',
            'https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=800&auto=format&fit=crop&q=80',
          ],
          amenities: ['Máy lạnh Inverter', 'Tủ lạnh', 'Gác lửng đúc', 'Tủ quần áo', 'Bếp từ mini'],
          description: 'Phòng studio mặt trước có ban công riêng view thoáng mát, đầy đủ nội thất chỉ cần xách vali vào ở.',
        ),
        const RoomItem(
          roomId: 204,
          roomCode: 'P.204',
          typeName: 'Phòng Đơn Có Gác Cao Ráo',
          price: 3200000,
          depositAmount: 3200000,
          area: 24.0,
          maxOccupancy: 2,
          status: 'Occupied',
          images: [
            'https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?w=800&auto=format&fit=crop&q=80',
          ],
          amenities: ['Máy lạnh', 'Gác lửng', 'Kệ bếp đá hoa cương', 'WC riêng'],
          description: 'Phòng có gác lửng cao không đụng đầu, quạt hút thông gió, kệ bếp tiện nghi.',
        ),
        const RoomItem(
          roomId: 301,
          roomCode: 'P.301',
          typeName: 'Studio Ban Công Tầng 3',
          price: 3500000,
          depositAmount: 3500000,
          area: 28.0,
          maxOccupancy: 2,
          status: 'Available',
          images: [
            'https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?w=800&auto=format&fit=crop&q=80',
          ],
          amenities: ['Máy lạnh', 'Tủ lạnh', 'Tủ quần áo âm tường', 'Nước nóng năng lượng mặt trời'],
          description: 'Phòng tầng 3 đón gió tự nhiên, ánh sáng chan hòa cả ngày, view ngắm hoàng hôn cực đẹp.',
        ),
      ],
    ),
    PropertyItem(
      propertyId: 2,
      propertyName: 'Căn Hộ Mini Xanh House',
      addressText: '88 Nguyễn Xí',
      ward: 'Phường 26',
      district: 'Bình Thạnh',
      city: 'TP. Hồ Chí Minh',
      location: const LatLng(10.8123, 106.7115),
      primaryImageUrl:
          'https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=800&auto=format&fit=crop&q=80',
      galleryUrls: [
        'https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=800&auto=format&fit=crop&q=80',
        'https://images.unsplash.com/photo-1536376072261-38c75010e6c9?w=800&auto=format&fit=crop&q=80',
      ],
      availableRoomCount: 2,
      startingPrice: 4200000,
      description:
          'Khu căn hộ mini phong cách hiện đại ngay chân cầu Đỏ Nguyễn Xí, thuận tiện di chuyển sang Quận 1, Gò Vấp và TP. Thủ Đức trong 10 phút. Có thang máy, bảo vệ 24/7.',
      amenities: defaultAmenities,
      livingCosts: defaultLivingCosts,
      rooms: [
        const RoomItem(
          roomId: 201,
          roomCode: 'X.201',
          typeName: 'Căn Hộ Mini 1 Phòng Ngủ',
          price: 4500000,
          depositAmount: 4500000,
          area: 32.0,
          maxOccupancy: 3,
          status: 'Available',
          images: [
            'https://images.unsplash.com/photo-1493809842364-78817add7ffb?w=800&auto=format&fit=crop&q=80',
          ],
          amenities: ['Smart TV', 'Máy lạnh Daikin', 'Bếp điện từ đôi', 'Sofa cao cấp'],
          description: 'Thiết kế căn hộ 1 phòng ngủ tách biệt khu vực tiếp khách và nấu ăn, không ám mùi.',
        ),
      ],
    ),
    PropertyItem(
      propertyId: 3,
      propertyName: 'Khu Trọ Làng Đại Học Sài Gòn',
      addressText: 'Số 18 Đường D1',
      ward: 'Tăng Nhơn Phú A',
      district: 'TP. Thủ Đức',
      city: 'TP. Hồ Chí Minh',
      location: const LatLng(10.8492, 106.7820),
      primaryImageUrl:
          'https://images.unsplash.com/photo-1554995207-c18c203602cb?w=800&auto=format&fit=crop&q=80',
      galleryUrls: [
        'https://images.unsplash.com/photo-1554995207-c18c203602cb?w=800&auto=format&fit=crop&q=80',
      ],
      availableRoomCount: 5,
      startingPrice: 2400000,
      description:
          'Dành riêng cho sinh viên FPTU, HUTECH, SPKT. Phòng trọ sạch sẽ, giá sinh viên, an ninh tuyệt đối, gần trạm xe buýt và các quán ăn bình dân.',
      amenities: defaultAmenities,
      livingCosts: defaultLivingCosts,
      rooms: [
        const RoomItem(
          roomId: 301,
          roomCode: 'D.102',
          typeName: 'Phòng Đơn Sinh Viên Giá Rẻ',
          price: 2400000,
          depositAmount: 2400000,
          area: 20.0,
          maxOccupancy: 2,
          status: 'Available',
          images: [
            'https://images.unsplash.com/photo-1554995207-c18c203602cb?w=800&auto=format&fit=crop&q=80',
          ],
          amenities: ['Gác lửng', 'Quạt trần', 'Wifi tốc độ cao', 'Chỗ phơi đồ riêng'],
          description: 'Phòng gọn gàng, tiết kiệm chi phí cho tân sinh viên và người mới đi làm.',
        ),
      ],
    ),
  ];

  // Danh sách thiết bị trong phòng của khách hiện tại (Phòng 204)
  static final List<EquipmentItem> myRoomEquipments = [
    const EquipmentItem(
      id: 1,
      name: 'Máy lạnh Panasonic Inverter 1.5 HP',
      code: 'EQ-AC-204',
      status: 'Tốt',
      installedDate: '15/01/2026',
    ),
    const EquipmentItem(
      id: 2,
      name: 'Tủ lạnh Aqua 90L 2 cánh',
      code: 'EQ-FR-204',
      status: 'Tốt',
      installedDate: '15/01/2026',
    ),
    const EquipmentItem(
      id: 3,
      name: 'Máy nước nóng trực tiếp Ariston',
      code: 'EQ-WH-204',
      status: 'Tốt',
      installedDate: '20/01/2026',
    ),
    const EquipmentItem(
      id: 4,
      name: 'Vòi sen tắm tăng áp Inox',
      code: 'EQ-SH-204',
      status: 'Cần sửa',
      installedDate: '20/01/2026',
    ),
  ];

  // Danh sách hóa đơn của khách thuê (Phòng 204)
  static final List<InvoiceModel> myInvoices = [
    InvoiceModel(
      invoiceId: 101,
      invoiceCode: 'HD-11-2026-204',
      monthYear: 'Tháng 11/2026',
      dueDate: DateTime.now().add(const Duration(days: 5)),
      totalAmount: 3462000,
      status: 'Unpaid',
      details: const [
        InvoiceDetailLine(
          title: 'Tiền thuê phòng',
          subtitle: 'Phòng 204 • Tháng 11/2026',
          amount: 3200000,
        ),
        InvoiceDetailLine(
          title: 'Tiền điện',
          subtitle: '52 kWh (Chỉ số: 1.428 → 1.480) x 3.500 ₫',
          amount: 182000,
          quantity: 52,
          unit: 'kWh',
        ),
        InvoiceDetailLine(
          title: 'Tiền nước sinh hoạt',
          subtitle: '4 m³ (Chỉ số: 210 → 214) x 20.000 ₫',
          amount: 80000,
          quantity: 4,
          unit: 'm³',
        ),
      ],
    ),
    InvoiceModel(
      invoiceId: 100,
      invoiceCode: 'HD-10-2026-204',
      monthYear: 'Tháng 10/2026',
      dueDate: DateTime(2026, 10, 10),
      totalAmount: 3410000,
      status: 'Paid',
      paidDate: DateTime(2026, 10, 5),
      paymentMethod: 'VNPay',
      details: const [
        InvoiceDetailLine(
          title: 'Tiền thuê phòng',
          subtitle: 'Phòng 204 • Tháng 10/2026',
          amount: 3200000,
        ),
        InvoiceDetailLine(
          title: 'Tiền điện',
          subtitle: '40 kWh (Chỉ số: 1.388 → 1.428) x 3.500 ₫',
          amount: 140000,
          quantity: 40,
          unit: 'kWh',
        ),
        InvoiceDetailLine(
          title: 'Tiền nước sinh hoạt',
          subtitle: '3.5 m³ (Chỉ số: 206.5 → 210) x 20.000 ₫',
          amount: 70000,
          quantity: 3.5,
          unit: 'm³',
        ),
      ],
    ),
    InvoiceModel(
      invoiceId: 99,
      invoiceCode: 'HD-09-2026-204',
      monthYear: 'Tháng 09/2026',
      dueDate: DateTime(2026, 9, 10),
      totalAmount: 3385000,
      status: 'Paid',
      paidDate: DateTime(2026, 9, 6),
      paymentMethod: 'MoMo',
      details: const [
        InvoiceDetailLine(
          title: 'Tiền thuê phòng',
          subtitle: 'Phòng 204 • Tháng 09/2026',
          amount: 3200000,
        ),
        InvoiceDetailLine(
          title: 'Tiền điện',
          subtitle: '35 kWh x 3.500 ₫',
          amount: 122500,
          quantity: 35,
          unit: 'kWh',
        ),
        InvoiceDetailLine(
          title: 'Tiền nước sinh hoạt',
          subtitle: '3.12 m³ x 20.000 ₫',
          amount: 62500,
          quantity: 3.12,
          unit: 'm³',
        ),
      ],
    ),
  ];
}
