import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';

class CustomerExploreScreen extends StatelessWidget {
  const CustomerExploreScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Search & Filter Header Placeholder
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                border: Border.all(color: AppColors.border),
                boxShadow: const [
                  BoxShadow(
                    color: Color(0x0A0F172A),
                    blurRadius: 10,
                    offset: Offset(0, 4),
                  ),
                ],
              ),
              child: Row(
                children: [
                  const Icon(Icons.search, color: AppColors.primary, size: 22),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      'Tìm theo quận, tên đường, khu vực...',
                      style: AppTypography.bodyMedium.copyWith(color: AppColors.textLight),
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: BorderRadius.circular(10),
                    ),
                    child: const Icon(Icons.tune_rounded, size: 18, color: AppColors.primary),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Quick Category Chips
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  _buildChip('Tất cả', isSelected: true),
                  _buildChip('Gò Vấp'),
                  _buildChip('Bình Thạnh'),
                  _buildChip('Quận 7'),
                  _buildChip('Thủ Đức'),
                  _buildChip('Dưới 3 triệu'),
                ],
              ),
            ),
            const SizedBox(height: 24),

            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Khu trọ nổi bật', style: AppTypography.headingMedium),
                Text('Xem tất cả',
                    style: AppTypography.labelMedium
                        .copyWith(color: AppColors.primary)),
              ],
            ),
            const SizedBox(height: 12),

            // Demo Property Card 1
            _buildPropertyCard(
              title: 'Nhà trọ Bình An',
              address: '12 Đường Số 5, Phường 10, Gò Vấp',
              price: 3200000,
              availableRooms: 3,
            ),
            const SizedBox(height: 16),

            // Demo Property Card 2
            _buildPropertyCard(
              title: 'Căn Hộ Mini Xanh House',
              address: '88 Nguyễn Xí, Phường 26, Bình Thạnh',
              price: 4500000,
              availableRooms: 2,
            ),
            const SizedBox(height: 20),
          ],
        ),
      ),
    );
  }

  Widget _buildChip(String label, {bool isSelected = false}) {
    return Container(
      margin: const EdgeInsets.only(right: 8),
      padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 7),
      decoration: BoxDecoration(
        color: isSelected ? AppColors.primary : AppColors.surfaceVariant,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        label,
        style: AppTypography.labelSmall.copyWith(
          color: isSelected ? Colors.white : AppColors.textSecondary,
        ),
      ),
    );
  }

  Widget _buildPropertyCard({
    required String title,
    required String address,
    required num price,
    required int availableRooms,
  }) {
    return AppCard(
      padding: EdgeInsets.zero,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            height: 140,
            decoration: const BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
            ),
            alignment: Alignment.center,
            child: const Icon(Icons.apartment_rounded,
                size: 48, color: AppColors.primary),
          ),
          Padding(
            padding: const EdgeInsets.all(14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(title, style: AppTypography.headingMedium),
                    StatusBadge(
                      label: '$availableRooms phòng trống',
                      tone: StatusTone.green,
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    const Icon(Icons.location_on_outlined,
                        size: 15, color: AppColors.textMuted),
                    const SizedBox(width: 4),
                    Expanded(
                      child: Text(address,
                          style: AppTypography.bodySmall,
                          overflow: TextOverflow.ellipsis),
                    ),
                  ],
                ),
                const Divider(height: 20),
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Text(
                      'Từ ${Formatters.formatShortPrice(price)}',
                      style: AppTypography.headingLarge
                          .copyWith(color: AppColors.primary),
                    ),
                    Text('Xem chi tiết →',
                        style: AppTypography.labelSmall
                            .copyWith(color: AppColors.primary)),
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
