import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';

class ManagerRoomsScreen extends StatefulWidget {
  const ManagerRoomsScreen({super.key});

  @override
  State<ManagerRoomsScreen> createState() => _ManagerRoomsScreenState();
}

class _ManagerRoomsScreenState extends State<ManagerRoomsScreen> {
  String _selectedFilter = 'Tất cả';

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          // Filter Tabs
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
            color: Colors.white,
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  _buildTabChip('Tất cả (35)'),
                  _buildTabChip('Trống (3)'),
                  _buildTabChip('Đang thuê (30)'),
                  _buildTabChip('Đã cọc (1)'),
                  _buildTabChip('Bảo trì (1)'),
                ],
              ),
            ),
          ),
          const Divider(height: 1),

          // Room List
          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                _buildRoomCard(
                  roomNumber: 'Phòng 101',
                  type: 'Studio Ban Công',
                  status: 'Available',
                  price: '3.5 tr/tháng',
                  floor: 'Tầng 1',
                  area: '28 m²',
                ),
                const SizedBox(height: 12),
                _buildRoomCard(
                  roomNumber: 'Phòng 204',
                  type: 'Phòng Đơn Có Gác',
                  status: 'Occupied',
                  tenant: 'Nguyễn Văn An (0901234567)',
                  price: '3.2 tr/tháng',
                  floor: 'Tầng 2',
                  area: '24 m²',
                ),
                const SizedBox(height: 12),
                _buildRoomCard(
                  roomNumber: 'Phòng 301',
                  type: 'Studio Ban Công',
                  status: 'Reserved',
                  tenant: 'Chờ ký HĐ • Cọc 3.5 tr',
                  price: '3.5 tr/tháng',
                  floor: 'Tầng 3',
                  area: '28 m²',
                ),
                const SizedBox(height: 12),
                _buildRoomCard(
                  roomNumber: 'Phòng 205',
                  type: 'Phòng Đơn Tiêu Chuẩn',
                  status: 'Maintenance',
                  price: '2.8 tr/tháng',
                  floor: 'Tầng 2',
                  area: '20 m²',
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTabChip(String label) {
    final cleanLabel = label.split(' ')[0];
    final isSelected = _selectedFilter == cleanLabel;

    return GestureDetector(
      onTap: () {
        setState(() => _selectedFilter = cleanLabel);
      },
      child: Container(
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
      ),
    );
  }

  Widget _buildRoomCard({
    required String roomNumber,
    required String type,
    required String status,
    String? tenant,
    required String price,
    required String floor,
    required String area,
  }) {
    return AppCard(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(8),
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: BorderRadius.circular(8),
                    ),
                    child: const Icon(Icons.meeting_room_rounded,
                        color: AppColors.primary, size: 20),
                  ),
                  const SizedBox(width: 10),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(roomNumber, style: AppTypography.headingSmall),
                      Text('$type • $floor • $area',
                          style: AppTypography.bodySmall),
                    ],
                  ),
                ],
              ),
              StatusBadge(label: status),
            ],
          ),
          if (tenant != null) ...[
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: AppColors.surfaceVariant,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                children: [
                  const Icon(Icons.person_pin_outlined,
                      size: 16, color: AppColors.textMuted),
                  const SizedBox(width: 6),
                  Expanded(
                    child: Text(
                      tenant,
                      style: AppTypography.bodySmall
                          .copyWith(fontWeight: FontWeight.w600),
                    ),
                  ),
                ],
              ),
            ),
          ],
          const Divider(height: 20),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(price,
                  style: AppTypography.labelLarge
                      .copyWith(color: AppColors.primary)),
              Text('Quản lý phòng →',
                  style: AppTypography.labelSmall
                      .copyWith(color: AppColors.primary)),
            ],
          ),
        ],
      ),
    );
  }
}
