import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/property_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/status_badge.dart';
import '../models/customer_models.dart';
import '../widgets/viewing_request_bottom_sheet.dart';

class RoomDetailScreen extends StatefulWidget {
  final PropertyItem property;
  final RoomItem room;

  const RoomDetailScreen({
    super.key,
    required this.property,
    required this.room,
  });

  @override
  State<RoomDetailScreen> createState() => _RoomDetailScreenState();
}

class _RoomDetailScreenState extends State<RoomDetailScreen> {
  final PropertyService _propertyService = PropertyService();
  late RoomItem _room;
  int _currentImageIndex = 0;

  @override
  void initState() {
    super.initState();
    _room = widget.room;
    _loadDetail();
  }

  Future<void> _loadDetail() async {
    try {
      final detail = await _propertyService.getRoomDetail(widget.room.roomId);
      if (mounted) setState(() => _room = detail);
    } catch (_) {}
  }

  void _openViewing() async {
    final success = await ViewingRequestBottomSheet.show(
      context,
      property: widget.property,
      room: _room,
    );
    if (success == true && mounted) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          backgroundColor: Color(0xFF087775),
          content: Text('Yêu cầu xem phòng đã được gửi thành công!'),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  void _goToDeposit() {
    context.push('/deposit', extra: {
      'property': widget.property,
      'room': _room,
    });
  }

  @override
  Widget build(BuildContext context) {
    final room = _room;
    final prop = widget.property;

    return Scaffold(
      backgroundColor: Colors.white,
      appBar: AppBar(
        backgroundColor: Colors.white,
        elevation: 0,
        leading: IconButton(
          icon: const Icon(Icons.arrow_back_ios_new_rounded,
              size: 18, color: AppColors.textPrimary),
          onPressed: () => context.pop(),
        ),
        actions: [
          IconButton(
            icon: Container(
              padding: const EdgeInsets.all(6),
              decoration: BoxDecoration(
                color: const Color(0xFFF1F5F9),
                shape: BoxShape.circle,
              ),
              child: const Icon(Icons.close_rounded,
                  size: 18, color: Color(0xFF64748B)),
            ),
            onPressed: () => context.pop(),
          ),
        ],
      ),
      body: SingleChildScrollView(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header: Property Name + Room Type Name
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 4),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    prop.propertyName.toUpperCase(),
                    style: const TextStyle(
                      color: Color(0xFF087775),
                      fontWeight: FontWeight.w700,
                      fontSize: 12,
                      letterSpacing: 1.0,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    room.typeName,
                    style: const TextStyle(
                      color: Color(0xFF0F172A),
                      fontWeight: FontWeight.w800,
                      fontSize: 22,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 12),

            // Image Carousel (Hình 1)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: ClipRRect(
                borderRadius: BorderRadius.circular(16),
                child: SizedBox(
                  height: 220,
                  child: Stack(
                    fit: StackFit.expand,
                    children: [
                      PageView.builder(
                        itemCount: room.images.isNotEmpty ? room.images.length : 1,
                        onPageChanged: (idx) =>
                            setState(() => _currentImageIndex = idx),
                        itemBuilder: (context, index) {
                          final url = room.images.isNotEmpty
                              ? room.images[index]
                              : prop.primaryImageUrl;
                          return url != null
                              ? Image.network(url, fit: BoxFit.cover)
                              : Container(
                                  color: const Color(0xFFE6F4F2),
                                  child: const Icon(Icons.meeting_room_rounded,
                                      size: 64, color: Color(0xFF087775)),
                                );
                        },
                      ),
                      if (room.images.length > 1)
                        Positioned(
                          bottom: 12,
                          right: 12,
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                                horizontal: 10, vertical: 4),
                            decoration: BoxDecoration(
                              color: Colors.black.withOpacity(0.6),
                              borderRadius: BorderRadius.circular(12),
                            ),
                            child: Text(
                              '${_currentImageIndex + 1}/${room.images.length}',
                              style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 12,
                                  fontWeight: FontWeight.bold),
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
            ),
            const SizedBox(height: 16),

            // Rent price, Area, Max Occupants
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Giá thuê',
                    style: TextStyle(color: Color(0xFF64748B), fontSize: 13),
                  ),
                  const SizedBox(height: 2),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.baseline,
                    textBaseline: TextBaseline.alphabetic,
                    children: [
                      Text(
                        Formatters.formatCurrency(room.price),
                        style: const TextStyle(
                          color: Color(0xFF087775),
                          fontWeight: FontWeight.w800,
                          fontSize: 24,
                        ),
                      ),
                      const Spacer(),
                      Text(
                        '${room.area > 0 ? room.area.round() : 19} m²',
                        style: const TextStyle(
                          fontSize: 13,
                          color: Color(0xFF64748B),
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                      const SizedBox(width: 16),
                      Text(
                        'Tối đa ${room.maxOccupancy > 0 ? room.maxOccupancy : 2} người',
                        style: const TextStyle(
                          fontSize: 13,
                          color: Color(0xFF64748B),
                          fontWeight: FontWeight.w500,
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Mô tả phòng
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Mô tả phòng',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    room.description.trim().isNotEmpty
                        ? room.description
                        : 'Chưa có mô tả',
                    style: const TextStyle(
                      fontSize: 14,
                      color: Color(0xFF64748B),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Địa chỉ
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Địa chỉ',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    prop.propertyName,
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                      color: Color(0xFF334155),
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    prop.fullAddress,
                    style: const TextStyle(
                      fontSize: 13,
                      color: Color(0xFF64748B),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Tiện ích phòng (Chips)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Tiện ích phòng',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: (room.amenities.isNotEmpty
                            ? room.amenities
                            : [
                                'Máy nước nóng',
                                'Cửa sổ',
                                'Tủ quần áo',
                                'Nhà vệ sinh riêng'
                              ])
                        .map((amenity) => _buildChip(amenity))
                        .toList(),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Dịch vụ (Bảng dịch vụ theo Hình 2)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Dịch vụ',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Container(
                    decoration: BoxDecoration(
                      color: const Color(0xFFF8FAFC),
                      borderRadius: BorderRadius.circular(12),
                      border: Border.all(color: const Color(0xFFE2E8F0)),
                    ),
                    padding: const EdgeInsets.symmetric(
                        horizontal: 16, vertical: 12),
                    child: Column(
                      children: [
                        _buildServiceRow('Electricity', '3.300 đ/kWh'),
                        const Divider(height: 18),
                        _buildServiceRow('Water', '21.000 đ/m³'),
                        const Divider(height: 18),
                        _buildServiceRow('Internet', '150.000 đ/phòng/tháng'),
                        const Divider(height: 18),
                        _buildServiceRow(
                            'Garbage Collection', '40.000 đ/phòng/tháng'),
                        const Divider(height: 18),
                        _buildServiceRow('Cleaning', '70.000 đ/phòng/tháng'),
                        const Divider(height: 18),
                        _buildServiceRow('Security', '70.000 đ/phòng/tháng'),
                      ],
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            // Thiết bị trong phòng (Chips theo Hình 2)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 20),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Thiết bị trong phòng',
                    style: TextStyle(
                      fontSize: 16,
                      fontWeight: FontWeight.w700,
                      color: Color(0xFF0F172A),
                    ),
                  ),
                  const SizedBox(height: 10),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      _buildChip('Quạt treo tường'),
                      _buildChip('Khóa cửa cơ'),
                    ],
                  ),
                ],
              ),
            ),
            const SizedBox(height: 100),
          ],
        ),
      ),

      // Bottom Bar với 2 nút: [Đặt lịch xem phòng] và [Đặt cọc giữ phòng]
      bottomNavigationBar: Container(
        padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
        decoration: BoxDecoration(
          color: Colors.white,
          border: const Border(top: BorderSide(color: Color(0xFFE2E8F0))),
          boxShadow: [
            BoxShadow(
              color: Colors.black.withOpacity(0.04),
              blurRadius: 10,
              offset: const Offset(0, -3),
            ),
          ],
        ),
        child: SafeArea(
          child: Row(
            children: [
              // Nút: Đặt lịch xem phòng
              Expanded(
                child: OutlinedButton(
                  onPressed: _openViewing,
                  style: OutlinedButton.styleFrom(
                    backgroundColor: const Color(0xFFF0FDF4),
                    side: const BorderSide(color: Color(0xFFCCFBF1), width: 1.5),
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  child: const Text(
                    'Đặt lịch xem phòng',
                    style: TextStyle(
                      color: Color(0xFF087775),
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                ),
              ),
              const SizedBox(width: 12),

              // Nút: Đặt cọc giữ phòng
              Expanded(
                child: ElevatedButton(
                  onPressed: room.isAvailable ? _goToDeposit : null,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: const Color(0xFF087775),
                    foregroundColor: Colors.white,
                    disabledBackgroundColor: const Color(0xFFCBD5E1),
                    padding: const EdgeInsets.symmetric(vertical: 14),
                    elevation: 0,
                    shape: RoundedRectangleBorder(
                      borderRadius: BorderRadius.circular(10),
                    ),
                  ),
                  child: const Text(
                    'Đặt cọc giữ phòng',
                    style: TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildChip(String label) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
      decoration: BoxDecoration(
        color: const Color(0xFFF1F5F9),
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: const TextStyle(
          fontSize: 13,
          fontWeight: FontWeight.w500,
          color: Color(0xFF334155),
        ),
      ),
    );
  }

  Widget _buildServiceRow(String name, String price) {
    return Row(
      mainAxisAlignment: MainAxisAlignment.spaceBetween,
      children: [
        Text(
          name,
          style: const TextStyle(
            fontSize: 13,
            color: Color(0xFF475569),
          ),
        ),
        Text(
          price,
          style: const TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.w600,
            color: Color(0xFF0F172A),
          ),
        ),
      ],
    );
  }
}
