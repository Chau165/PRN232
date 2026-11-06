import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../../../routes/app_routes.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';

class ManagerDashboardScreen extends StatefulWidget {
  const ManagerDashboardScreen({super.key});

  @override
  State<ManagerDashboardScreen> createState() => _ManagerDashboardScreenState();
}

class _ManagerDashboardScreenState extends State<ManagerDashboardScreen> {
  late List<ManagerTaskModel> _urgentTasks;

  @override
  void initState() {
    super.initState();
    _urgentTasks = List.from(ManagerMockData.tasks);
  }

  int get _occupiedCount =>
      ManagerMockData.rooms.where((r) => r.isOccupied).length;

  int get _availableCount =>
      ManagerMockData.rooms.where((r) => r.isAvailable).length;

  int get _pendingTaskCount =>
      _urgentTasks.where((t) => t.isPending).length;

  void _handleApproveTask(ManagerTaskModel task) {
    setState(() {
      task.status = TaskStatus.approved;
    });
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã phê duyệt "${task.title}" thành công!'),
        backgroundColor: AppColors.primary,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _handleRejectTask(ManagerTaskModel task) {
    setState(() {
      task.status = TaskStatus.rejected;
    });
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã từ chối "${task.title}".'),
        backgroundColor: AppColors.danger,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final totalRooms = ManagerMockData.rooms.length;
    final occupancyRate = ((_occupiedCount / totalRooms) * 100).toStringAsFixed(1);

    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              ManagerMockData.currentPropertyName,
              style: AppTypography.headingMedium,
            ),
            Text(
              'Quản lý vận hành • Tháng 11/2026',
              style: AppTypography.bodySmall
                  .copyWith(color: AppColors.textSecondary),
            ),
          ],
        ),
        backgroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Thông báo',
            icon: const Icon(Icons.notifications_outlined,
                color: AppColors.textPrimary),
            onPressed: () => context.push(AppRoutes.notifications),
          ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // 4 KPI Cards
            GridView.count(
              shrinkWrap: true,
              physics: const NeverScrollableScrollPhysics(),
              crossAxisCount: 2,
              crossAxisSpacing: 12,
              mainAxisSpacing: 12,
              childAspectRatio: 1.35,
              children: [
                _buildKpiCard(
                  label: 'Doanh thu tháng',
                  value: '40.2 tr',
                  subtext: 'Đã thu: 28.4 tr (70%)',
                  icon: Icons.monetization_on_outlined,
                  color: AppColors.primary,
                  bg: AppColors.primaryLight,
                ),
                _buildKpiCard(
                  label: 'Tỷ lệ lấp đầy',
                  value: '$occupancyRate%',
                  subtext: 'Đang thuê: $_occupiedCount/$totalRooms phòng',
                  icon: Icons.meeting_room_outlined,
                  color: AppColors.success,
                  bg: AppColors.successBg,
                ),
                _buildKpiCard(
                  label: 'Phòng trống',
                  value: '$_availableCount phòng',
                  subtext: 'Sẵn sàng đón khách mới',
                  icon: Icons.vpn_key_outlined,
                  color: AppColors.info,
                  bg: AppColors.infoBg,
                ),
                _buildKpiCard(
                  label: 'Công việc chờ',
                  value: '$_pendingTaskCount việc',
                  subtext: 'Cần duyệt cọc & bảo trì',
                  icon: Icons.assignment_late_outlined,
                  color: AppColors.warning,
                  bg: AppColors.warningBg,
                ),
              ],
            ),
            const SizedBox(height: 20),

            // Quick Operations Card
            AppCard(
              color: AppColors.primary,
              border: BorderSide.none,
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: Colors.white.withValues(alpha: 0.2),
                      shape: BoxShape.circle,
                    ),
                    child: const Icon(Icons.speed_rounded,
                        color: Colors.white, size: 26),
                  ),
                  const SizedBox(width: 14),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          'Ghi chỉ số Điện / Nước',
                          style: AppTypography.headingSmall
                              .copyWith(color: Colors.white),
                        ),
                        Text(
                          'Còn 7 phòng chưa chốt số tháng 11',
                          style: AppTypography.bodySmall
                              .copyWith(color: Colors.white70),
                        ),
                      ],
                    ),
                  ),
                  ElevatedButton(
                    style: ElevatedButton.styleFrom(
                      backgroundColor: Colors.white,
                      foregroundColor: AppColors.primary,
                      elevation: 0,
                      padding: const EdgeInsets.symmetric(
                          horizontal: 14, vertical: 8),
                      minimumSize: const Size(70, 36),
                    ),
                    onPressed: () {
                      context.go(AppRoutes.managerBilling);
                    },
                    child: Text('Ghi ngay',
                        style: AppTypography.labelSmall
                            .copyWith(color: AppColors.primary)),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 14),

            // Quick Operations Row
            Row(
              children: [
                Expanded(
                  child: InkWell(
                    onTap: () => context.push(AppRoutes.managerContracts),
                    borderRadius: BorderRadius.circular(12),
                    child: Container(
                      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: AppColors.border),
                      ),
                      child: Column(
                        children: [
                          const Icon(Icons.description_outlined, color: AppColors.primary, size: 22),
                          const SizedBox(height: 4),
                          Text('Hợp đồng', style: AppTypography.labelSmall.copyWith(fontWeight: FontWeight.bold)),
                        ],
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: InkWell(
                    onTap: () => context.push(AppRoutes.managerEquipments),
                    borderRadius: BorderRadius.circular(12),
                    child: Container(
                      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: AppColors.border),
                      ),
                      child: Column(
                        children: [
                          const Icon(Icons.devices_other_rounded, color: AppColors.primary, size: 22),
                          const SizedBox(height: 4),
                          Text('Thiết bị', style: AppTypography.labelSmall.copyWith(fontWeight: FontWeight.bold)),
                        ],
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 8),
                Expanded(
                  child: InkWell(
                    onTap: () => context.push(AppRoutes.managerIdentityReviews),
                    borderRadius: BorderRadius.circular(12),
                    child: Container(
                      padding: const EdgeInsets.symmetric(vertical: 12, horizontal: 8),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(color: AppColors.border),
                      ),
                      child: Column(
                        children: [
                          const Icon(Icons.verified_user_outlined, color: AppColors.primary, size: 22),
                          const SizedBox(height: 4),
                          Text('Duyệt CCCD', style: AppTypography.labelSmall.copyWith(fontWeight: FontWeight.bold)),
                        ],
                      ),
                    ),
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),

            // Urgent Tasks Section
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Công việc cần xử lý ngay',
                    style: AppTypography.headingMedium),
                TextButton(
                  onPressed: () => context.go(AppRoutes.managerTasks),
                  child: Text('Xem tất cả →',
                      style: AppTypography.labelMedium
                          .copyWith(color: AppColors.primary)),
                ),
              ],
            ),
            const SizedBox(height: 10),

            if (_urgentTasks.isEmpty)
              const AppCard(
                child: Center(
                  child: Text('Không có công việc nào cần xử lý!'),
                ),
              )
            else
              ListView.separated(
                shrinkWrap: true,
                physics: const NeverScrollableScrollPhysics(),
                itemCount: _urgentTasks.take(3).length,
                separatorBuilder: (context, index) =>
                    const SizedBox(height: 12),
                itemBuilder: (context, index) {
                  final task = _urgentTasks[index];
                  return _buildDashboardTaskCard(task);
                },
              ),
            const SizedBox(height: 24),
          ],
        ),
      ),
    );
  }

  Widget _buildKpiCard({
    required String label,
    required String value,
    required String subtext,
    required IconData icon,
    required Color color,
    required Color bg,
  }) {
    return AppCard(
      padding: const EdgeInsets.all(14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text(label, style: AppTypography.bodySmall),
              Container(
                padding: const EdgeInsets.all(6),
                decoration: BoxDecoration(color: bg, shape: BoxShape.circle),
                child: Icon(icon, color: color, size: 18),
              ),
            ],
          ),
          Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(value,
                  style: AppTypography.headingLarge.copyWith(color: color)),
              const SizedBox(height: 2),
              Text(subtext,
                  style: AppTypography.bodySmall.copyWith(fontSize: 10),
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis),
            ],
          ),
        ],
      ),
    );
  }

  Widget _buildDashboardTaskCard(ManagerTaskModel task) {
    String badgeText;
    StatusTone tone;
    if (task.type == TaskType.deposit) {
      badgeText = 'Duyệt cọc';
      tone = StatusTone.teal;
    } else if (task.type == TaskType.viewing) {
      badgeText = 'Xem phòng';
      tone = StatusTone.amber;
    } else {
      badgeText = 'Bảo trì';
      tone = task.priority == 'Khẩn cấp' ? StatusTone.rose : StatusTone.amber;
    }

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
                    padding: const EdgeInsets.symmetric(
                        horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      task.roomCode,
                      style: AppTypography.labelSmall
                          .copyWith(color: AppColors.primary),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text(task.customerName, style: AppTypography.labelLarge),
                ],
              ),
              StatusBadge(label: badgeText, tone: tone),
            ],
          ),
          const SizedBox(height: 6),
          Text(task.title, style: AppTypography.bodyMedium),
          Text(task.description,
              style: AppTypography.bodySmall
                  .copyWith(color: AppColors.textSecondary)),
          if (task.isPending) ...[
            const Divider(height: 20),
            Row(
              children: [
                Expanded(
                  child: AppButton(
                    text: 'Từ chối',
                    height: 36,
                    variant: AppButtonVariant.outline,
                    onPressed: () => _handleRejectTask(task),
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: AppButton(
                    text: 'Phê duyệt',
                    height: 36,
                    onPressed: () => _handleApproveTask(task),
                  ),
                ),
              ],
            ),
          ] else ...[
            const SizedBox(height: 8),
            Row(
              children: [
                Icon(
                  task.isApproved
                      ? Icons.check_circle_outline_rounded
                      : Icons.cancel_outlined,
                  size: 16,
                  color: task.isApproved ? AppColors.success : AppColors.danger,
                ),
                const SizedBox(width: 6),
                Text(
                  task.isApproved ? 'Đã phê duyệt' : 'Đã từ chối',
                  style: AppTypography.bodySmall.copyWith(
                    color:
                        task.isApproved ? AppColors.success : AppColors.danger,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }
}
