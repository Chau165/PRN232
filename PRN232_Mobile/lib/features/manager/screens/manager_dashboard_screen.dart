import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';

class ManagerDashboardScreen extends StatelessWidget {
  const ManagerDashboardScreen({super.key});

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Header stats greeting
            Text('Tổng quan vận hành', style: AppTypography.headingLarge),
            const SizedBox(height: 4),
            Text('Nhà trọ Bình An • 32/35 phòng đang thuê',
                style: AppTypography.bodySmall),
            const SizedBox(height: 16),

            // 4 Grid KPI Cards
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
                  value: '42.5 tr',
                  subtext: '+8% so với tháng trước',
                  icon: Icons.monetization_on_outlined,
                  color: AppColors.primary,
                  bg: AppColors.primaryLight,
                ),
                _buildKpiCard(
                  label: 'Phòng trống',
                  value: '3 phòng',
                  subtext: 'Tỉ lệ lấp đầy: 91.4%',
                  icon: Icons.meeting_room_outlined,
                  color: AppColors.success,
                  bg: AppColors.successBg,
                ),
                _buildKpiCard(
                  label: 'Lịch hẹn xem',
                  value: '4 lịch',
                  subtext: '2 lịch trong hôm nay',
                  icon: Icons.event_available_outlined,
                  color: AppColors.info,
                  bg: AppColors.infoBg,
                ),
                _buildKpiCard(
                  label: 'Cần sửa chữa',
                  value: '2 sự cố',
                  subtext: '1 sự cố khẩn cấp',
                  icon: Icons.build_circle_outlined,
                  color: AppColors.danger,
                  bg: AppColors.dangerBg,
                ),
              ],
            ),
            const SizedBox(height: 24),

            // Urgent Tasks section
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                Text('Việc cần xử lý ngay', style: AppTypography.headingMedium),
                const StatusBadge(label: '3 việc chờ', tone: StatusTone.amber),
              ],
            ),
            const SizedBox(height: 12),

            AppCard(
              child: Column(
                children: [
                  _buildTaskTile(
                    title: 'Duyệt cọc phòng 302',
                    desc: 'Khách Nguyễn Văn B đã chuyển cọc 3.5 tr',
                    time: '15 phút trước',
                    badge: 'Cọc phòng',
                    tone: StatusTone.teal,
                  ),
                  const Divider(height: 16),
                  _buildTaskTile(
                    title: 'Hẹn xem phòng Studio 101',
                    desc: 'Lê Thu Trang hẹn xem lúc 16:30 chiều nay',
                    time: '1 giờ trước',
                    badge: 'Xem phòng',
                    tone: StatusTone.amber,
                  ),
                  const Divider(height: 16),
                  _buildTaskTile(
                    title: 'Báo hỏng vòi sen phòng 205',
                    desc: 'Rò rỉ nước cần gán thợ xử lý',
                    time: '2 giờ trước',
                    badge: 'Bảo trì',
                    tone: StatusTone.rose,
                  ),
                ],
              ),
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

  Widget _buildTaskTile({
    required String title,
    required String desc,
    required String time,
    required String badge,
    required StatusTone tone,
  }) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Expanded(
                    child: Text(title,
                        style: AppTypography.labelLarge,
                        overflow: TextOverflow.ellipsis),
                  ),
                  StatusBadge(label: badge, tone: tone),
                ],
              ),
              const SizedBox(height: 3),
              Text(desc, style: AppTypography.bodySmall),
              const SizedBox(height: 2),
              Text(time,
                  style: AppTypography.bodySmall
                      .copyWith(fontSize: 11, color: AppColors.textLight)),
            ],
          ),
        ),
      ],
    );
  }
}
