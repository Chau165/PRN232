import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';
import '../widgets/report_damage_bottom_sheet.dart';

class MyMaintenanceRequestsScreen extends StatefulWidget {
  const MyMaintenanceRequestsScreen({super.key});

  @override
  State<MyMaintenanceRequestsScreen> createState() =>
      _MyMaintenanceRequestsScreenState();
}

class _MyMaintenanceRequestsScreenState
    extends State<MyMaintenanceRequestsScreen> {
  late List<CustomerMaintenanceModel> _requests;

  @override
  void initState() {
    super.initState();
    _requests = List.from(CustomerMockData.myMaintenanceRequests);
  }

  void _createNewReport() async {
    final success = await ReportDamageBottomSheet.show(context);
    if (success == true) {
      setState(() {
        _requests.insert(
          0,
          CustomerMaintenanceModel(
            id: DateTime.now().millisecondsSinceEpoch,
            title: 'Sự cố thiết bị mới báo',
            description: 'Đã tiếp nhận yêu cầu và đang chờ Quản lý điều phối thợ kỹ thuật.',
            roomCode: 'P.204',
            propertyName: 'Nhà trọ Bình An',
            priority: 'Khẩn cấp',
            status: 'Pending',
            createdAt: DateTime.now(),
          ),
        );
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Lịch Sử Báo Hỏng & Bảo Trì'),
        backgroundColor: Colors.white,
        elevation: 0,
        actions: [
          IconButton(
            tooltip: 'Báo sự cố mới',
            icon: const Icon(Icons.add_circle_outline_rounded,
                color: AppColors.primary),
            onPressed: _createNewReport,
          ),
        ],
      ),
      body: _requests.isEmpty
          ? Center(
              child: Column(
                mainAxisAlignment: MainAxisAlignment.center,
                children: [
                  const Icon(Icons.handyman_outlined,
                      size: 48, color: AppColors.textMuted),
                  const SizedBox(height: 12),
                  Text('Bạn chưa có yêu cầu sửa chữa nào',
                      style: AppTypography.bodyMedium
                          .copyWith(color: AppColors.textSecondary)),
                ],
              ),
            )
          : ListView.separated(
              padding: const EdgeInsets.all(16),
              itemCount: _requests.length,
              separatorBuilder: (_, _) => const SizedBox(height: 14),
              itemBuilder: (context, index) {
                final item = _requests[index];
                return _buildMaintenanceCard(item);
              },
            ),
    );
  }

  Widget _buildMaintenanceCard(CustomerMaintenanceModel item) {
    StatusTone tone;
    String statusLabel;

    if (item.isCompleted) {
      tone = StatusTone.green;
      statusLabel = 'Đã hoàn tất';
    } else if (item.isInProgress) {
      tone = StatusTone.teal;
      statusLabel = 'Đang sửa chữa';
    } else {
      tone = StatusTone.amber;
      statusLabel = 'Đang chờ thợ';
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
                    padding:
                        const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                    decoration: BoxDecoration(
                      color: AppColors.primaryLight,
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      item.roomCode,
                      style: AppTypography.labelSmall
                          .copyWith(color: AppColors.primary),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Text(item.title, style: AppTypography.headingSmall),
                ],
              ),
              StatusBadge(label: statusLabel, tone: tone),
            ],
          ),
          const SizedBox(height: 6),
          Text(item.description, style: AppTypography.bodySmall),
          const SizedBox(height: 8),
          Text(
            'Thời gian gửi: ${Formatters.formatDateTime(item.createdAt)} • Mức độ: ${item.priority}',
            style: AppTypography.bodySmall
                .copyWith(color: AppColors.textSecondary, fontSize: 11),
          ),

          // Technician banner if assigned
          if (item.technicianName != null) ...[
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: item.isCompleted
                    ? AppColors.successBg
                    : AppColors.primaryLight,
                borderRadius: BorderRadius.circular(8),
              ),
              child: Row(
                children: [
                  Icon(
                    item.isCompleted
                        ? Icons.check_circle_rounded
                        : Icons.engineering_rounded,
                    color: item.isCompleted
                        ? AppColors.success
                        : AppColors.primary,
                    size: 20,
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          item.isCompleted
                              ? 'Nghiệm thu: Hoàn tất sửa chữa'
                              : 'Thợ phụ trách: ${item.technicianName}',
                          style: AppTypography.labelMedium.copyWith(
                            color: item.isCompleted
                                ? AppColors.successText
                                : AppColors.primary,
                          ),
                        ),
                        if (item.technicianPhone != null && !item.isCompleted)
                          Text('SĐT liên hệ: ${item.technicianPhone}',
                              style: AppTypography.bodySmall),
                        if (item.actualCost != null && item.isCompleted)
                          Text(
                            'Chi phí: ${Formatters.formatCurrency(item.actualCost!)} (Chủ trọ chi trả)',
                            style: AppTypography.bodySmall.copyWith(
                                color: AppColors.textPrimary,
                                fontWeight: FontWeight.bold),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}
