import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../models/customer_models.dart';

class PropertyCardWidget extends StatelessWidget {
  final PropertyItem property;
  final VoidCallback onTap;

  const PropertyCardWidget({
    super.key,
    required this.property,
    required this.onTap,
  });

  @override
  Widget build(BuildContext context) {
    return AppCard(
      padding: EdgeInsets.zero,
      onTap: onTap,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Primary Image Header
          Stack(
            children: [
              Container(
                height: 160,
                width: double.infinity,
                color: AppColors.primaryLight,
                child: property.primaryImageUrl != null
                    ? Image.network(
                        property.primaryImageUrl!,
                        fit: BoxFit.cover,
                        errorBuilder: (_, _, _) => const Center(
                          child: Icon(Icons.apartment_rounded,
                              size: 48, color: AppColors.primary),
                        ),
                      )
                    : const Center(
                        child: Icon(Icons.apartment_rounded,
                            size: 48, color: AppColors.primary),
                      ),
              ),
              Positioned(
                top: 12,
                right: 12,
                child: StatusBadge(
                  label: '${property.availableRoomCount} phòng trống',
                  tone: StatusTone.green,
                ),
              ),
            ],
          ),

          // Content body
          Padding(
            padding: const EdgeInsets.all(14),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  property.propertyName,
                  style: AppTypography.headingMedium,
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                ),
                const SizedBox(height: 4),
                Row(
                  children: [
                    const Icon(Icons.location_on_outlined,
                        size: 15, color: AppColors.textMuted),
                    const SizedBox(width: 4),
                    Expanded(
                      child: Text(
                        property.fullAddress,
                        style: AppTypography.bodySmall,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),

                // Amenity Chips Preview
                Wrap(
                  spacing: 6,
                  runSpacing: 4,
                  children: property.amenities.take(3).map((amenity) {
                    return Container(
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 3),
                      decoration: BoxDecoration(
                        color: AppColors.surfaceVariant,
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        amenity.name,
                        style: AppTypography.bodySmall.copyWith(
                          fontSize: 10,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    );
                  }).toList(),
                ),

                const Divider(height: 20),

                // Price and action
                Row(
                  mainAxisAlignment: MainAxisAlignment.spaceBetween,
                  children: [
                    Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text('Giá thuê từ',
                            style: AppTypography.bodySmall
                                .copyWith(fontSize: 11)),
                        Text(
                          Formatters.formatShortPrice(property.startingPrice),
                          style: AppTypography.headingLarge
                              .copyWith(color: AppColors.primary),
                        ),
                      ],
                    ),
                    Row(
                      children: [
                        Text(
                          'Xem chi tiết',
                          style: AppTypography.labelMedium
                              .copyWith(color: AppColors.primary),
                        ),
                        const SizedBox(width: 2),
                        const Icon(Icons.arrow_forward_rounded,
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
