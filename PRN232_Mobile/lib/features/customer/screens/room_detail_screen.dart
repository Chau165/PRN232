import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/property_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
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
          backgroundColor: AppColors.success,
          content: Text('Yêu cầu xem phòng đã được gửi đến Quản lý!'),
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
      backgroundColor: AppColors.background,
      body: CustomScrollView(
        slivers: [
          SliverAppBar(
            expandedHeight: 250,
            pinned: true,
            leading: IconButton(
              icon: Container(
                padding: const EdgeInsets.all(6),
                decoration: const BoxDecoration(
                  color: Colors.white,
                  shape: BoxShape.circle,
                ),
                child: const Icon(Icons.arrow_back_ios_new_rounded,
                    size: 16, color: AppColors.textPrimary),
              ),
              onPressed: () => context.pop(),
            ),
            flexibleSpace: FlexibleSpaceBar(
              background: Stack(
                fit: StackFit.expand,
                children: [
                  PageView.builder(
                    itemCount:
                        room.images.isNotEmpty ? room.images.length : 1,
                    onPageChanged: (idx) =>
                        setState(() => _currentImageIndex = idx),
                    itemBuilder: (context, index) {
                      final url = room.images.isNotEmpty
                          ? room.images[index]
                          : prop.primaryImageUrl;
                      return url != null
                          ? Image.network(url, fit: BoxFit.cover)
                          : Container(
                              color: AppColors.primaryLight,
                              child: const Icon(Icons.meeting_room_rounded,
                                  size: 64, color: AppColors.primary),
                            );
                    },
                  ),
                  if (room.images.length > 1)
                    Positioned(
                      bottom: 16,
                      right: 16,
                      child: Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 10, vertical: 4),
                        decoration: BoxDecoration(
                          color: Colors.black.withValues(alpha: 0.6),
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

          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text(room.roomCode, style: AppTypography.displayLarge),
                      StatusBadge(
                        label: room.isAvailable ? 'Còn phòng' : 'Đang thuê',
                        tone: room.isAvailable
                            ? StatusTone.green
                            : StatusTone.slate,
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(room.typeName, style: AppTypography.headingSmall),
                  Text('${prop.propertyName} • ${prop.fullAddress}',
                      style: AppTypography.bodySmall),
                  const Divider(height: 28),

                  // Specifications Grid
                  Row(
                    children: [
                      Expanded(
                        child: _buildSpecBox('Giá thuê/tháng',
                            Formatters.formatCurrency(room.price), Icons.payments_outlined),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: _buildSpecBox('Tiền cọc giữ chỗ',
                            Formatters.formatCurrency(room.depositAmount), Icons.lock_clock_outlined),
                      ),
                    ],
                  ),
                  const SizedBox(height: 10),
                  Row(
                    children: [
                      Expanded(
                        child: _buildSpecBox('Diện tích sử dụng',
                            Formatters.formatArea(room.area), Icons.square_foot_outlined),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: _buildSpecBox('Sức chứa tối đa',
                            '${room.maxOccupancy} người', Icons.people_outline_rounded),
                      ),
                    ],
                  ),
                  const SizedBox(height: 24),

                  // Room Description
                  Text('Mô tả chi tiết phòng',
                      style: AppTypography.headingSmall),
                  const SizedBox(height: 8),
                  Text(room.description, style: AppTypography.bodyMedium),
                  const SizedBox(height: 24),

                  // Room Amenities
                  Text('Trang thiết bị & Tiện nghi phòng',
                      style: AppTypography.headingSmall),
                  const SizedBox(height: 12),
                  AppCard(
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      children: room.amenities.map((amenity) {
                        return Padding(
                          padding: const EdgeInsets.symmetric(vertical: 6),
                          child: Row(
                            children: [
                              Container(
                                padding: const EdgeInsets.all(6),
                                decoration: const BoxDecoration(
                                  color: AppColors.primaryLight,
                                  shape: BoxShape.circle,
                                ),
                                child: const Icon(Icons.check_rounded,
                                    size: 14, color: AppColors.primary),
                              ),
                              const SizedBox(width: 12),
                              Text(amenity, style: AppTypography.labelLarge),
                            ],
                          ),
                        );
                      }).toList(),
                    ),
                  ),
                  const SizedBox(height: 80),
                ],
              ),
            ),
          ),
        ],
      ),

      bottomNavigationBar: Container(
        padding: const EdgeInsets.all(16),
        decoration: const BoxDecoration(
          color: Colors.white,
          border: Border(top: BorderSide(color: AppColors.borderLight)),
        ),
        child: SafeArea(
          child: Row(
            children: [
              Expanded(
                child: AppButton(
                  text: 'Hẹn xem phòng',
                  variant: AppButtonVariant.outline,
                  icon: const Icon(Icons.calendar_today_rounded,
                      size: 16, color: AppColors.primary),
                  onPressed: _openViewing,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: AppButton(
                  text: 'Đặt cọc giữ chỗ',
                  icon: const Icon(Icons.bookmark_add_rounded,
                      size: 16, color: Colors.white),
                  onPressed: room.isAvailable ? _goToDeposit : null,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildSpecBox(String label, String value, IconData icon) {
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: AppColors.surfaceVariant,
        borderRadius: BorderRadius.circular(12),
        border: Border.all(color: AppColors.borderLight),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(icon, size: 16, color: AppColors.primary),
              const SizedBox(width: 6),
              Text(label,
                  style: AppTypography.bodySmall.copyWith(fontSize: 11)),
            ],
          ),
          const SizedBox(height: 4),
          Text(value,
              style: AppTypography.headingSmall
                  .copyWith(fontSize: 14, color: AppColors.textPrimary)),
        ],
      ),
    );
  }
}
