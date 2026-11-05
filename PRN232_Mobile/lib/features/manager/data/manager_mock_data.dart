import '../models/manager_models.dart';

class ManagerMockData {
  ManagerMockData._();

  static const String currentPropertyName = 'Nhà trọ Bình An';
  static const String currentPropertyAddress =
      '12 Đường Số 5, Phường 17, Quận Gò Vấp, TP.HCM';

  // 1. Danh sách thợ kỹ thuật đối tác
  static final List<TechnicianModel> technicians = [
    const TechnicianModel(
      id: 1,
      name: 'Nguyễn Văn Nam',
      phone: '0912 345 678',
      specialty: 'Điện lạnh & Điều hòa',
      rating: 4.9,
      isAvailable: true,
    ),
    const TechnicianModel(
      id: 2,
      name: 'Lê Văn Hùng',
      phone: '0987 654 321',
      specialty: 'Cấp thoát nước & Vệ sinh',
      rating: 4.8,
      isAvailable: true,
    ),
    const TechnicianModel(
      id: 3,
      name: 'Trần Đình Trọng',
      phone: '0933 221 100',
      specialty: 'Thiết bị điện & Khóa từ',
      rating: 4.7,
      isAvailable: false,
    ),
    const TechnicianModel(
      id: 4,
      name: 'Phạm Quốc Bảo',
      phone: '0944 556 677',
      specialty: 'Sửa chữa mộc & Nội thất',
      rating: 4.9,
      isAvailable: true,
    ),
  ];

  // 2. Danh sách phòng quản lý (12 phòng tại Nhà trọ Bình An)
  static final List<ManagerRoomModel> rooms = [
    // Tầng 1
    ManagerRoomModel(
      roomId: 101,
      roomCode: 'P.101',
      typeName: 'Studio Ban Công',
      floor: 1,
      area: 28.0,
      price: 3500000,
      depositAmount: 3500000,
      status: 'Available',
      tenants: const [],
    ),
    ManagerRoomModel(
      roomId: 102,
      roomCode: 'P.102',
      typeName: 'Phòng Đơn Có Gác',
      floor: 1,
      area: 24.0,
      price: 3200000,
      depositAmount: 3200000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 1,
          name: 'Trần Minh Quân',
          phone: '0903 112 233',
          cccd: '079201004567',
          startDate: DateTime(2025, 8, 15),
        ),
      ],
      contractEnd: DateTime(2026, 8, 15),
    ),
    ManagerRoomModel(
      roomId: 103,
      roomCode: 'P.103',
      typeName: 'Phòng Tiêu Chuẩn',
      floor: 1,
      area: 20.0,
      price: 2800000,
      depositAmount: 2800000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 2,
          name: 'Đặng Thùy Dung',
          phone: '0918 223 344',
          cccd: '079202008899',
          startDate: DateTime(2026, 1, 1),
        ),
      ],
      contractEnd: DateTime(2026, 12, 31),
    ),
    ManagerRoomModel(
      roomId: 104,
      roomCode: 'P.104',
      typeName: 'Studio Ban Công',
      floor: 1,
      area: 28.0,
      price: 3500000,
      depositAmount: 3500000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 3,
          name: 'Lê Hữu Đạt',
          phone: '0979 556 677',
          cccd: '079201011223',
          startDate: DateTime(2026, 3, 10),
        ),
      ],
      contractEnd: DateTime(2027, 3, 10),
    ),

    // Tầng 2
    ManagerRoomModel(
      roomId: 201,
      roomCode: 'P.201',
      typeName: 'Studio Ban Công',
      floor: 2,
      area: 28.0,
      price: 3600000,
      depositAmount: 3600000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 4,
          name: 'Vũ Thị Ngọc',
          phone: '0938 445 566',
          cccd: '079202033445',
          startDate: DateTime(2025, 11, 1),
        ),
      ],
      contractEnd: DateTime(2026, 11, 1),
    ),
    ManagerRoomModel(
      roomId: 202,
      roomCode: 'P.202',
      typeName: 'Phòng Đơn Có Gác',
      floor: 2,
      area: 24.0,
      price: 3200000,
      depositAmount: 3200000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 5,
          name: 'Hoàng Văn Đức',
          phone: '0908 667 788',
          cccd: '079201055667',
          startDate: DateTime(2026, 5, 20),
        ),
      ],
      contractEnd: DateTime(2027, 5, 20),
    ),
    ManagerRoomModel(
      roomId: 203,
      roomCode: 'P.203',
      typeName: 'Phòng Tiêu Chuẩn',
      floor: 2,
      area: 22.0,
      price: 2900000,
      depositAmount: 2900000,
      status: 'Available',
      tenants: const [],
    ),
    ManagerRoomModel(
      roomId: 204,
      roomCode: 'P.204',
      typeName: 'Phòng Đơn Có Gác',
      floor: 2,
      area: 25.0,
      price: 3200000,
      depositAmount: 3200000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 6,
          name: 'Nguyễn Văn An',
          phone: '0901 234 567',
          cccd: '079201001234',
          startDate: DateTime(2026, 1, 1),
        ),
      ],
      contractEnd: DateTime(2026, 12, 31),
    ),

    // Tầng 3
    ManagerRoomModel(
      roomId: 301,
      roomCode: 'P.301',
      typeName: 'Studio Ban Công',
      floor: 3,
      area: 30.0,
      price: 3700000,
      depositAmount: 3700000,
      status: 'Reserved',
      tenants: const [],
    ),
    ManagerRoomModel(
      roomId: 302,
      roomCode: 'P.302',
      typeName: 'Phòng Đơn Có Gác',
      floor: 3,
      area: 24.0,
      price: 3200000,
      depositAmount: 3200000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 7,
          name: 'Phạm Minh Tuấn',
          phone: '0982 778 899',
          cccd: '079201099887',
          startDate: DateTime(2026, 2, 1),
        ),
      ],
      contractEnd: DateTime(2027, 2, 1),
    ),
    ManagerRoomModel(
      roomId: 303,
      roomCode: 'P.303',
      typeName: 'Phòng Tiêu Chuẩn',
      floor: 3,
      area: 20.0,
      price: 2800000,
      depositAmount: 2800000,
      status: 'Maintenance',
      tenants: const [],
    ),
    ManagerRoomModel(
      roomId: 304,
      roomCode: 'P.304',
      typeName: 'Phòng Đơn Có Gác',
      floor: 3,
      area: 25.0,
      price: 3300000,
      depositAmount: 3300000,
      status: 'Occupied',
      tenants: [
        TenantModel(
          id: 8,
          name: 'Bùi Lan Anh',
          phone: '0915 667 711',
          cccd: '079202077889',
          startDate: DateTime(2025, 10, 15),
        ),
      ],
      contractEnd: DateTime(2026, 10, 15),
    ),
  ];

  // 3. Danh sách các công việc Manager cần xử lý
  static final List<ManagerTaskModel> tasks = [
    // Task 1: Duyệt cọc P.301
    ManagerTaskModel(
      id: 101,
      type: TaskType.deposit,
      status: TaskStatus.pending,
      title: 'Duyệt cọc phòng P.301',
      description: 'Khách Nguyễn Văn B đã chuyển khoản cọc qua Cổng VNPAY.',
      customerName: 'Nguyễn Văn B',
      customerPhone: '0909 988 776',
      roomCode: 'P.301',
      propertyName: 'Nhà trọ Bình An',
      createdAt: DateTime.now().subtract(const Duration(minutes: 25)),
      targetDate: DateTime.now().add(const Duration(days: 7)),
      amount: 3700000,
      paymentMethod: 'Cổng VNPAY (ATM/QR)',
    ),

    // Task 2: Hẹn xem phòng P.101
    ManagerTaskModel(
      id: 102,
      type: TaskType.viewing,
      status: TaskStatus.pending,
      title: 'Lịch hẹn xem phòng P.101',
      description: 'Khách muốn xem ban công và chỗ gửi xe máy tầng hầm.',
      customerName: 'Lê Thu Trang',
      customerPhone: '0988 776 655',
      roomCode: 'P.101',
      propertyName: 'Nhà trọ Bình An',
      createdAt: DateTime.now().subtract(const Duration(hours: 1)),
      targetDate: DateTime.now().add(const Duration(hours: 3)), // 16:30 chiều nay
    ),

    // Task 3: Hẹn xem phòng P.203
    ManagerTaskModel(
      id: 103,
      type: TaskType.viewing,
      status: TaskStatus.pending,
      title: 'Lịch hẹn xem phòng P.203',
      description: 'Muốn xem trực tiếp phòng trước khi quyết định đặt cọc.',
      customerName: 'Phạm Hoàng Nam',
      customerPhone: '0977 112 233',
      roomCode: 'P.203',
      propertyName: 'Nhà trọ Bình An',
      createdAt: DateTime.now().subtract(const Duration(hours: 3)),
      targetDate: DateTime.now().add(const Duration(days: 1)), // 10:00 ngày mai
    ),

    // Task 4: Báo hỏng vòi sen P.204
    ManagerTaskModel(
      id: 104,
      type: TaskType.maintenance,
      status: TaskStatus.pending,
      title: 'Báo hỏng vòi sen tắm & van xả',
      description:
          'Khách báo vòi nước tắm bị rò rỉ chảy liên tục gây lãng phí nước.',
      customerName: 'Nguyễn Văn An',
      customerPhone: '0901 234 567',
      roomCode: 'P.204',
      propertyName: 'Nhà trọ Bình An',
      createdAt: DateTime.now().subtract(const Duration(hours: 2)),
      priority: 'Khẩn cấp',
      evidenceImages: const [
        'https://images.unsplash.com/photo-1584622650111-993a426fbf0a?auto=format&fit=crop&w=600&q=80',
      ],
    ),

    // Task 5: Báo hỏng máy lạnh P.102
    ManagerTaskModel(
      id: 105,
      type: TaskType.maintenance,
      status: TaskStatus.inProgress,
      title: 'Máy lạnh không lạnh & chảy nước',
      description: 'Máy lạnh Panasonic phà hơi nóng, cần kiểm tra gas và bảo dưỡng.',
      customerName: 'Trần Minh Quân',
      customerPhone: '0903 112 233',
      roomCode: 'P.102',
      propertyName: 'Nhà trọ Bình An',
      createdAt: DateTime.now().subtract(const Duration(days: 1)),
      priority: 'Bình thường',
      technicianName: 'Nguyễn Văn Nam',
      evidenceImages: const [
        'https://images.unsplash.com/photo-1621905251918-48416bd8575a?auto=format&fit=crop&w=600&q=80',
      ],
    ),
  ];

  // 4. Danh sách ghi chỉ số điện nước tháng 11/2026
  static final List<MeterReadingModel> meterReadings = [
    MeterReadingModel(
      roomId: 101,
      roomCode: 'P.101',
      floor: 'Tầng 1',
      oldElectricity: 1240,
      oldWater: 88,
      isRecorded: false,
    ),
    MeterReadingModel(
      roomId: 102,
      roomCode: 'P.102',
      floor: 'Tầng 1',
      oldElectricity: 2150,
      newElectricity: 2265, // +115 kWh
      oldWater: 142,
      newWater: 146, // +4 m3
      recordDate: DateTime.now().subtract(const Duration(hours: 4)),
      isRecorded: true,
    ),
    MeterReadingModel(
      roomId: 103,
      roomCode: 'P.103',
      floor: 'Tầng 1',
      oldElectricity: 1890,
      newElectricity: 1980, // +90 kWh
      oldWater: 95,
      newWater: 98, // +3 m3
      recordDate: DateTime.now().subtract(const Duration(hours: 3)),
      isRecorded: true,
    ),
    MeterReadingModel(
      roomId: 104,
      roomCode: 'P.104',
      floor: 'Tầng 1',
      oldElectricity: 1450,
      oldWater: 110,
      isRecorded: false,
    ),
    MeterReadingModel(
      roomId: 201,
      roomCode: 'P.201',
      floor: 'Tầng 2',
      oldElectricity: 3100,
      newElectricity: 3240,
      oldWater: 210,
      newWater: 215,
      recordDate: DateTime.now().subtract(const Duration(hours: 2)),
      isRecorded: true,
    ),
    MeterReadingModel(
      roomId: 202,
      roomCode: 'P.202',
      floor: 'Tầng 2',
      oldElectricity: 2780,
      oldWater: 165,
      isRecorded: false,
    ),
    MeterReadingModel(
      roomId: 203,
      roomCode: 'P.203',
      floor: 'Tầng 2',
      oldElectricity: 950,
      oldWater: 60,
      isRecorded: false,
    ),
    MeterReadingModel(
      roomId: 204,
      roomCode: 'P.204',
      floor: 'Tầng 2',
      oldElectricity: 2450,
      newElectricity: 2568, // +118 kWh
      oldWater: 172,
      newWater: 176, // +4 m3
      recordDate: DateTime.now().subtract(const Duration(hours: 1)),
      isRecorded: true,
    ),
    MeterReadingModel(
      roomId: 301,
      roomCode: 'P.301',
      floor: 'Tầng 3',
      oldElectricity: 1100,
      oldWater: 75,
      isRecorded: false,
    ),
    MeterReadingModel(
      roomId: 302,
      roomCode: 'P.302',
      floor: 'Tầng 3',
      oldElectricity: 2300,
      oldWater: 150,
      isRecorded: false,
    ),
    MeterReadingModel(
      roomId: 303,
      roomCode: 'P.303',
      floor: 'Tầng 3',
      oldElectricity: 1800,
      oldWater: 120,
      isRecorded: false,
    ),
    MeterReadingModel(
      roomId: 304,
      roomCode: 'P.304',
      floor: 'Tầng 3',
      oldElectricity: 2650,
      newElectricity: 2775,
      oldWater: 190,
      newWater: 195,
      recordDate: DateTime.now().subtract(const Duration(minutes: 40)),
      isRecorded: true,
    ),
  ];

  // 5. Danh sách hóa đơn của khu trọ (Tháng 11/2026)
  static final List<ManagerInvoiceModel> invoices = [
    ManagerInvoiceModel(
      invoiceId: 501,
      invoiceCode: 'HD-11-204',
      roomCode: 'P.204',
      tenantName: 'Nguyễn Văn An',
      amount: 3462000,
      status: 'Unpaid',
      dueDate: DateTime.now().add(const Duration(days: 5)),
      monthYear: '11/2026',
    ),
    ManagerInvoiceModel(
      invoiceId: 502,
      invoiceCode: 'HD-11-102',
      roomCode: 'P.102',
      tenantName: 'Trần Minh Quân',
      amount: 3820000,
      status: 'Paid',
      dueDate: DateTime.now().add(const Duration(days: 5)),
      monthYear: '11/2026',
      paymentMethod: 'Cổng VNPAY',
    ),
    ManagerInvoiceModel(
      invoiceId: 503,
      invoiceCode: 'HD-11-201',
      roomCode: 'P.201',
      tenantName: 'Vũ Thị Ngọc',
      amount: 3950000,
      status: 'Paid',
      dueDate: DateTime.now().add(const Duration(days: 5)),
      monthYear: '11/2026',
      paymentMethod: 'Tiền mặt (Đã thu)',
    ),
    ManagerInvoiceModel(
      invoiceId: 504,
      invoiceCode: 'HD-11-202',
      roomCode: 'P.202',
      tenantName: 'Hoàng Văn Đức',
      amount: 3580000,
      status: 'Overdue',
      dueDate: DateTime.now().subtract(const Duration(days: 2)),
      monthYear: '11/2026',
    ),
    ManagerInvoiceModel(
      invoiceId: 505,
      invoiceCode: 'HD-11-103',
      roomCode: 'P.103',
      tenantName: 'Đặng Thùy Dung',
      amount: 3120000,
      status: 'Unpaid',
      dueDate: DateTime.now().add(const Duration(days: 5)),
      monthYear: '11/2026',
    ),
    ManagerInvoiceModel(
      invoiceId: 506,
      invoiceCode: 'HD-11-304',
      roomCode: 'P.304',
      tenantName: 'Bùi Lan Anh',
      amount: 3670000,
      status: 'Paid',
      dueDate: DateTime.now().add(const Duration(days: 5)),
      monthYear: '11/2026',
      paymentMethod: 'Ví MoMo',
    ),
  ];
}
