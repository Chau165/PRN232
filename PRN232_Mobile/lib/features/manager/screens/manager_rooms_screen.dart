import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';
import '../widgets/room_detail_manage_sheet.dart';

class ManagerRoomsScreen extends StatefulWidget {
  const ManagerRoomsScreen({super.key});

  @override
  State<ManagerRoomsScreen> createState() => _ManagerRoomsScreenState();
}

class _ManagerRoomsScreenState extends State<ManagerRoomsScreen> {
  late List<ManagerRoomModel> _rooms;
  String _selectedFilter = 'Tất cả';

  final List<String> _filters = [
    'Tất cả',
    'Đang thuê',
    'Trống',
    'Đã cọc',
    'Bảo trì',
  ];

  @override
  void initState() {
    super.initState();
    _rooms = List.from(ManagerMockData.rooms);
  }

  List<ManagerRoomModel> get _filteredRooms {
    if (_selectedFilter == 'Đang thuê') {
      return _rooms.where((r) => r.isOccupied).toList();
    } else if (_selectedFilter == 'Trống') {
      return _rooms.where((r) => r.isAvailable).toList();
    } else if (_selectedFilter == 'Đã cọc') {
      return _rooms.where((r) => r.isReserved).toList();
    } else if (_selectedFilter == 'Bảo trì') {
      return _rooms.where((r) => r.isMaintenance).toList();
    }
    return _rooms;
  }

  void _openRoomManage(ManagerRoomModel room) {
    RoomDetailManageSheet.show(
      context,
      room: room,
      onStatusChanged: (newStatus) {
        setState(() {});
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    // Phân nhóm phòng theo tầng
    final floors = [1, 2, 3];

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Phòng & Khách Thuê'),
        backgroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Thêm phòng mới',
            icon: const Icon(Icons.add_circle_outline_rounded,
                color: AppColors.primary),
            onPressed: () {
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(
                  content: Text('Tính năng Thêm phòng mới đang được cấu hình!'),
                  behavior: SnackBarBehavior.floating,
                ),
              );
            },
          ),
        ],
      ),
      body: Column(
        children: [
          // Filter Chips
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            color: Colors.white,
            child: SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: _filters.map((filter) {
                  final isSelected = _selectedFilter == filter;
                  int count = 0;
                  if (filter == 'Tất cả') {
                    count = _rooms.length;
                  } else if (filter == 'Đang thuê') {
                    count = _rooms.where((r) => r.isOccupied).length;
                  } else if (filter == 'Trống') {
                    count = _rooms.where((r) => r.isAvailable).length;
                  } else if (filter == 'Đã cọc') {
                    count = _rooms.where((r) => r.isReserved).length;
                  } else if (filter == 'Bảo trì') {
                    count = _rooms.where((r) => r.isMaintenance).length;
                  }

                  return Padding(
                    padding: const EdgeInsets.only(right: 8),
                    child: ChoiceChip(
                      label: Text('$filter ($count)'),
                      selected: isSelected,
                      selectedColor: AppColors.primary,
                      backgroundColor: AppColors.surfaceVariant,
                      labelStyle: AppTypography.labelSmall.copyWith(
                        color: isSelected ? Colors.white : AppColors.textPrimary,
                        fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
                      ),
                      side: BorderSide(
                        color: isSelected ? AppColors.primary : AppColors.border,
                      ),
                      onSelected: (selected) {
                        if (selected) {
                          setState(() => _selectedFilter = filter);
                        }
                      },
                    ),
                  );
                }).toList(),
              ),
            ),
          ),
          const Divider(height: 1),

          // List by Floors
          Expanded(
            child: _filteredRooms.isEmpty
                ? Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.search_off_rounded,
                            size: 48, color: AppColors.textMuted),
                        const SizedBox(height: 12),
                        Text('Không tìm thấy phòng phù hợp bộ lọc',
                            style: AppTypography.bodyMedium
                                .copyWith(color: AppColors.textSecondary)),
                      ],
                    ),
                  )
                : ListView(
                    padding: const EdgeInsets.all(16),
                    children: floors.map((floorNum) {
                      final floorRooms = _filteredRooms
                          .where((r) => r.floor == floorNum)
                          .toList();

                      if (floorRooms.isEmpty) return const SizedBox.shrink();

                      return Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Padding(
                            padding: const EdgeInsets.symmetric(vertical: 8),
                            child: Row(
                              children: [
                                Container(
                                  width: 4,
                                  height: 16,
                                  decoration: BoxDecoration(
                                    color: AppColors.primary,
                                    borderRadius: BorderRadius.circular(2),
                                  ),
                                ),
                                const SizedBox(width: 8),
                                Text('TẦNG $floorNum',
                                    style: AppTypography.labelLarge.copyWith(
                                        color: AppColors.textPrimary,
                                        fontWeight: FontWeight.bold)),
                                const SizedBox(width: 8),
                                Text('(${floorRooms.length} phòng)',
                                    style: AppTypography.bodySmall
                                        .copyWith(color: AppColors.textMuted)),
                              ],
                            ),
                          ),
                          ...floorRooms.map(
                            (room) => Padding(
                              padding: const EdgeInsets.only(bottom: 12),
                              child: _buildRoomItemCard(room),
                            ),
                          ),
                          const SizedBox(height: 12),
                        ],
                      );
                    }).toList(),
                  ),
          ),
        ],
      ),
    );
  }

  Widget _buildRoomItemCard(ManagerRoomModel room) {
    return AppCard(
      onTap: () => _openRoomManage(room),
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
                      Text(room.roomCode, style: AppTypography.headingSmall),
                      Text('${room.typeName} • ${room.area} m²',
                          style: AppTypography.bodySmall),
                    ],
                  ),
                ],
              ),
              StatusBadge(label: room.status),
            ],
          ),
          if (room.isOccupied) ...[
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
              decoration: BoxDecoration(
                color: AppColors.surfaceVariant,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                children: [
                  const Icon(Icons.person_pin_outlined,
                      size: 16, color: AppColors.textMuted),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      '${room.primaryTenantName} • ${room.primaryTenantPhone}',
                      style: AppTypography.bodySmall
                          .copyWith(fontWeight: FontWeight.w600),
                    ),
                  ),
                  const Icon(Icons.chevron_right_rounded,
                      size: 18, color: AppColors.textMuted),
                ],
              ),
            ),
          ] else if (room.isReserved) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: AppColors.infoBg,
                borderRadius: BorderRadius.circular(6),
              ),
              child: Row(
                children: [
                  const Icon(Icons.lock_clock_outlined,
                      size: 16, color: AppColors.info),
                  const SizedBox(width: 6),
                  Text('Đã nhận cọc • Chờ dọn vào ký hợp đồng',
                      style: AppTypography.bodySmall
                          .copyWith(color: AppColors.infoText)),
                ],
              ),
            ),
          ],
          const Divider(height: 20),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(
                Formatters.formatCurrency(room.price),
                style: AppTypography.labelLarge
                    .copyWith(color: AppColors.primary, fontWeight: FontWeight.bold),
              ),
              Text('Quản lý chi tiết →',
                  style: AppTypography.labelSmall
                      .copyWith(color: AppColors.primary)),
            ],
          ),
        ],
      ),
    );
  }
}
