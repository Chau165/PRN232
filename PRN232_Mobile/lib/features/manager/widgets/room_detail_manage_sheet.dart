import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../models/manager_models.dart';

class RoomDetailManageSheet extends StatefulWidget {
  final ManagerRoomModel room;
  final ValueChanged<String>? onStatusChanged;

  const RoomDetailManageSheet({
    super.key,
    required this.room,
    this.onStatusChanged,
  });

  static Future<void> show(
    BuildContext context, {
    required ManagerRoomModel room,
    ValueChanged<String>? onStatusChanged,
  }) {
    return AppBottomSheet.show(
      context: context,
      title: 'Quản lý ${room.roomCode}',
      subtitle: '${room.typeName} • Tầng ${room.floor}',
      child: RoomDetailManageSheet(
        room: room,
        onStatusChanged: onStatusChanged,
      ),
    );
  }

  @override
  State<RoomDetailManageSheet> createState() => _RoomDetailManageSheetState();
}

class _RoomDetailManageSheetState extends State<RoomDetailManageSheet> {
  late String _currentStatus;

  @override
  void initState() {
    super.initState();
    _currentStatus = widget.room.status;
  }

  void _changeStatus(String newStatus) {
    setState(() => _currentStatus = newStatus);
    widget.onStatusChanged?.call(newStatus);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
            'Đã cập nhật trạng thái phòng ${widget.room.roomCode} thành "$newStatus"'),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Room status & price card
        AppCard(
          color: AppColors.primaryLight,
          border: const BorderSide(color: Color(0xFFC7E7E2)),
          child: Column(
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(widget.room.roomCode,
                          style: AppTypography.displayMedium
                              .copyWith(color: AppColors.primary)),
                      Text('${widget.room.typeName} • ${widget.room.area} m²',
                          style: AppTypography.bodySmall),
                    ],
                  ),
                  StatusBadge(label: _currentStatus),
                ],
              ),
              const Divider(height: 20),
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text('Giá thuê niêm yết',
                          style: AppTypography.bodySmall),
                      Text(Formatters.formatCurrency(widget.room.price),
                          style: AppTypography.headingSmall),
                    ],
                  ),
                  Column(
                    crossAxisAlignment: CrossAxisAlignment.end,
                    children: [
                      Text('Tiền đặt cọc giữ chỗ',
                          style: AppTypography.bodySmall),
                      Text(Formatters.formatCurrency(widget.room.depositAmount),
                          style: AppTypography.labelLarge
                              .copyWith(color: AppColors.primary)),
                    ],
                  ),
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),

        // Quick status changer
        Text('Thay đổi trạng thái phòng',
            style: AppTypography.headingSmall),
        const SizedBox(height: 10),
        Row(
          children: [
            _buildStatusChip('Available', 'Trống'),
            const SizedBox(width: 8),
            _buildStatusChip('Occupied', 'Đang thuê'),
            const SizedBox(width: 8),
            _buildStatusChip('Maintenance', 'Bảo trì'),
          ],
        ),
        const SizedBox(height: 24),

        // Tenants list
        Text('Thông tin khách đang thuê',
            style: AppTypography.headingSmall),
        const SizedBox(height: 10),
        if (widget.room.tenants.isEmpty)
          AppCard(
            padding: const EdgeInsets.all(20),
            child: Center(
              child: Column(
                children: [
                  const Icon(Icons.person_off_outlined,
                      size: 36, color: AppColors.textMuted),
                  const SizedBox(height: 8),
                  Text('Phòng hiện chưa có khách thuê',
                      style: AppTypography.bodyMedium
                          .copyWith(color: AppColors.textSecondary)),
                ],
              ),
            ),
          )
        else
          ...widget.room.tenants.map(
            (t) => AppCard(
              margin: const EdgeInsets.only(bottom: 10),
              child: Column(
                children: [
                  Row(
                    children: [
                      CircleAvatar(
                        backgroundColor: AppColors.primary,
                        radius: 20,
                        child: Text(
                          t.name.substring(0, 1),
                          style: const TextStyle(
                              color: Colors.white, fontWeight: FontWeight.bold),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Row(
                              children: [
                                Text(t.name, style: AppTypography.labelLarge),
                                if (t.isPrimary) ...[
                                  const SizedBox(width: 6),
                                  const StatusBadge(
                                    label: 'Đại diện HĐ',
                                    tone: StatusTone.teal,
                                  ),
                                ],
                              ],
                            ),
                            const SizedBox(height: 2),
                            Text('SĐT: ${t.phone} • CCCD: ${t.cccd}',
                                style: AppTypography.bodySmall),
                            Text(
                              'Bắt đầu thuê: ${Formatters.formatDate(t.startDate)}',
                              style: AppTypography.bodySmall
                                  .copyWith(color: AppColors.textMuted),
                            ),
                          ],
                        ),
                      ),
                    ],
                  ),
                  const Divider(height: 16),
                  Row(
                    children: [
                      Expanded(
                        child: OutlinedButton.icon(
                          icon: const Icon(Icons.phone_in_talk_rounded, size: 16),
                          label: const Text('Gọi điện'),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: AppColors.primary,
                            side: const BorderSide(color: AppColors.primary),
                          ),
                          onPressed: () {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text('Đang gọi tới ${t.phone}...'),
                                behavior: SnackBarBehavior.floating,
                              ),
                            );
                          },
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: OutlinedButton.icon(
                          icon: const Icon(Icons.chat_bubble_outline_rounded,
                              size: 16),
                          label: const Text('Nhắn tin'),
                          style: OutlinedButton.styleFrom(
                            foregroundColor: AppColors.textSecondary,
                            side: const BorderSide(color: AppColors.border),
                          ),
                          onPressed: () {
                            ScaffoldMessenger.of(context).showSnackBar(
                              SnackBar(
                                content: Text('Đang mở tin nhắn tới ${t.phone}...'),
                                behavior: SnackBarBehavior.floating,
                              ),
                            );
                          },
                        ),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          ),
        const SizedBox(height: 20),

        AppButton(
          text: 'Đóng',
          variant: AppButtonVariant.outline,
          onPressed: () => Navigator.pop(context),
        ),
        const SizedBox(height: 10),
      ],
    );
  }

  Widget _buildStatusChip(String statusCode, String label) {
    final isSelected = _currentStatus == statusCode;
    return Expanded(
      child: InkWell(
        onTap: () => _changeStatus(statusCode),
        borderRadius: BorderRadius.circular(8),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 8),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primary : AppColors.surfaceVariant,
            borderRadius: BorderRadius.circular(8),
            border: Border.all(
              color: isSelected ? AppColors.primary : AppColors.border,
            ),
          ),
          alignment: Alignment.center,
          child: Text(
            label,
            style: AppTypography.labelSmall.copyWith(
              color: isSelected ? Colors.white : AppColors.textPrimary,
              fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
            ),
          ),
        ),
      ),
    );
  }
}
