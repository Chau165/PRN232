import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../models/customer_models.dart';
import '../widgets/viewing_request_bottom_sheet.dart';

class PropertyDetailScreen extends StatefulWidget {
  final PropertyItem property;

  const PropertyDetailScreen({
    super.key,
    required this.property,
  });

  @override
  State<PropertyDetailScreen> createState() => _PropertyDetailScreenState();
}

class _PropertyDetailScreenState extends State<PropertyDetailScreen> {
  int _currentImageIndex = 0;

  void _openViewingRequest() async {
    final success = await ViewingRequestBottomSheet.show(
      context,
      property: widget.property,
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

  @override
  Widget build(BuildContext context) {
    final prop = widget.property;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: CustomScrollView(
        slivers: [
          // Collapsible Image Carousel App Bar
          SliverAppBar(
            expandedHeight: 260,
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
                    itemCount: prop.galleryUrls.isNotEmpty
                        ? prop.galleryUrls.length
                        : 1,
                    onPageChanged: (idx) =>
                        setState(() => _currentImageIndex = idx),
                    itemBuilder: (context, index) {
                      final url = prop.galleryUrls.isNotEmpty
                          ? prop.galleryUrls[index]
                          : prop.primaryImageUrl;
                      return url != null
                          ? Image.network(url, fit: BoxFit.cover)
                          : Container(
                              color: AppColors.primaryLight,
                              child: const Icon(Icons.apartment_rounded,
                                  size: 64, color: AppColors.primary),
                            );
                    },
                  ),
                  if (prop.galleryUrls.length > 1)
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
                          '${_currentImageIndex + 1}/${prop.galleryUrls.length}',
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

          // Details Body
          SliverToBoxAdapter(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  // Title & Price
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(prop.propertyName,
                            style: AppTypography.displayMedium),
                      ),
                      StatusBadge(
                        label: '${prop.availableRoomCount} phòng trống',
                        tone: StatusTone.green,
                      ),
                    ],
                  ),
                  const SizedBox(height: 6),
                  Row(
                    children: [
                      const Icon(Icons.location_on_outlined,
                          size: 16, color: AppColors.primary),
                      const SizedBox(width: 4),
                      Expanded(
                        child: Text(prop.fullAddress,
                            style: AppTypography.bodySmall),
                      ),
                    ],
                  ),
                  const Divider(height: 28),

                  // Description
                  Text('Giới thiệu khu trọ', style: AppTypography.headingSmall),
                  const SizedBox(height: 8),
                  Text(prop.description, style: AppTypography.bodyMedium),
                  const SizedBox(height: 24),

                  // Common Amenities
                  Text('Tiện ích tòa nhà', style: AppTypography.headingSmall),
                  const SizedBox(height: 12),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: prop.amenities.map((item) {
                      return Container(
                        padding: const EdgeInsets.symmetric(
                            horizontal: 12, vertical: 7),
                        decoration: BoxDecoration(
                          color: AppColors.surfaceVariant,
                          borderRadius: BorderRadius.circular(999),
                        ),
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.check_circle_rounded,
                                size: 14, color: AppColors.primary),
                            const SizedBox(width: 6),
                            Text(item.name,
                                style: AppTypography.labelSmall
                                    .copyWith(color: AppColors.textPrimary)),
                          ],
                        ),
                      );
                    }).toList(),
                  ),
                  const SizedBox(height: 24),

                  // Living Costs
                  Text('Chi phí dịch vụ sinh hoạt',
                      style: AppTypography.headingSmall),
                  const SizedBox(height: 10),
                  AppCard(
                    padding: EdgeInsets.zero,
                    child: Column(
                      children: prop.livingCosts.map((cost) {
                        return Column(
                          children: [
                            Padding(
                              padding: const EdgeInsets.symmetric(
                                  horizontal: 14, vertical: 10),
                              child: Row(
                                mainAxisAlignment:
                                    MainAxisAlignment.spaceBetween,
                                children: [
                                  Column(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      Text(cost.serviceName,
                                          style: AppTypography.labelLarge),
                                      Text(cost.basis,
                                          style: AppTypography.bodySmall
                                              .copyWith(fontSize: 11)),
                                    ],
                                  ),
                                  Text(
                                    '${Formatters.formatCurrency(cost.price)} / ${cost.unit}',
                                    style: AppTypography.headingSmall.copyWith(
                                        color: AppColors.primary,
                                        fontSize: 14),
                                  ),
                                ],
                              ),
                            ),
                            if (cost != prop.livingCosts.last)
                              const Divider(height: 1),
                          ],
                        );
                      }).toList(),
                    ),
                  ),
                  const SizedBox(height: 28),

                  // Rooms in this Property
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Text('Danh sách phòng',
                          style: AppTypography.headingSmall),
                      Text('${prop.rooms.length} loại phòng',
                          style: AppTypography.bodySmall),
                    ],
                  ),
                  const SizedBox(height: 12),

                  ListView.separated(
                    shrinkWrap: true,
                    physics: const NeverScrollableScrollPhysics(),
                    itemCount: prop.rooms.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 12),
                    itemBuilder: (context, index) {
                      final room = prop.rooms[index];
                      return _buildRoomTile(context, room, prop);
                    },
                  ),
                  const SizedBox(height: 80),
                ],
              ),
            ),
          ),
        ],
      ),

      // Fixed Bottom Action Bar
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
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Giá chỉ từ',
                        style: AppTypography.bodySmall.copyWith(fontSize: 11)),
                    Text(
                      Formatters.formatShortPrice(prop.startingPrice),
                      style: AppTypography.headingLarge
                          .copyWith(color: AppColors.primary),
                    ),
                  ],
                ),
              ),
              AppButton(
                text: 'Hẹn xem phòng',
                width: 170,
                icon: const Icon(Icons.calendar_today_rounded,
                    size: 16, color: Colors.white),
                onPressed: _openViewingRequest,
              ),
            ],
          ),
        ),
      ),
    );
  }

  Widget _buildRoomTile(
      BuildContext context, RoomItem room, PropertyItem property) {
    return AppCard(
      onTap: () {
        context.push('/room-detail', extra: {
          'property': property,
          'room': room,
        });
      },
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(10),
            child: Container(
              width: 90,
              height: 90,
              color: AppColors.primaryLight,
              child: room.images.isNotEmpty
                  ? Image.network(room.images.first, fit: BoxFit.cover)
                  : const Icon(Icons.meeting_room_rounded,
                      color: AppColors.primary),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(room.roomCode, style: AppTypography.headingSmall),
                    StatusBadge(
                      label: room.isAvailable ? 'Còn phòng' : 'Đang thuê',
                      tone: room.isAvailable
                          ? StatusTone.green
                          : StatusTone.slate,
                    ),
                  ],
                ),
                const SizedBox(height: 3),
                Text(
                  '${Formatters.formatArea(room.area)} • Tối đa ${room.maxOccupancy} người',
                  style: AppTypography.bodySmall,
                ),
                const SizedBox(height: 6),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      Formatters.formatCurrency(room.price),
                      style: AppTypography.labelLarge
                          .copyWith(color: AppColors.primary),
                    ),
                    Row(
                      children: [
                        Text('Chi tiết',
                            style: AppTypography.labelSmall
                                .copyWith(color: AppColors.primary)),
                        const Icon(Icons.chevron_right,
                            size: 16, color: AppColors.primary),
                      ],
                    ),
                  ],
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}
