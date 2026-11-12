import 'package:flutter/material.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/maintenance_service.dart';
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
  final MaintenanceService _maintenanceService = MaintenanceService();
  List<CustomerMaintenanceModel> _requests = [];
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _requests = AuthState.isCurrentDemo ? List.from(CustomerMockData.myMaintenanceRequests) : [];
    _loadData();
  }

  Future<void> _loadData() async {
    setState(() => _isLoading = true);
    try {
      final list = await _maintenanceService.getCustomerRequests();
      if (mounted) {
        setState(() {
          _requests = list;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _createNewReport() async {
    final success = await ReportDamageBottomSheet.show(context);
    if (success == true) {
      _loadData();
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
      body: _isLoading
          ? const Center(child: CircularProgressIndicator(color: AppColors.primary))
          : RefreshIndicator(
              onRefresh: _loadData,
              color: AppColors.primary,
              child: _requests.isEmpty
                  ? Center(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          const Icon(Icons.task_alt_rounded,
                              size: 64, color: AppColors.textMuted),
                          const SizedBox(height: 12),
                          Text('Không có yêu cầu bảo trì nào',
                              style: AppTypography.bodyMedium),
                          const SizedBox(height: 4),
                          Text('Phòng và thiết bị của bạn đang hoạt động bình thường.',
                              style: AppTypography.bodySmall),
                        ],
                      ),
                    )
                  : ListView.separated(
                      padding: const EdgeInsets.all(16),
                      itemCount: _requests.length,
                      separatorBuilder: (context, index) =>
                          const SizedBox(height: 12),
                      itemBuilder: (context, index) {
                        final req = _requests[index];
                        final isCompleted = req.isCompleted;
                        final isInProgress = req.isInProgress;

                        return AppCard(
                          padding: const EdgeInsets.all(16),
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
                                          color: isCompleted
                                              ? AppColors.successBg
                                              : (isInProgress
                                                  ? AppColors.warningBg
                                                  : AppColors.surfaceVariant),
                                          shape: BoxShape.circle,
                                        ),
                                        child: Icon(
                                          isCompleted
                                              ? Icons.check_rounded
                                              : (isInProgress
                                                  ? Icons.build_circle_outlined
                                                  : Icons.hourglass_top_rounded),
                                          size: 18,
                                          color: isCompleted
                                              ? AppColors.success
                                              : (isInProgress
                                                  ? AppColors.warning
                                                  : AppColors.textSecondary),
                                        ),
                                      ),
                                      const SizedBox(width: 10),
                                      Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Text(req.title,
                                              style: AppTypography.labelLarge),
                                          Text(
                                            '${req.roomCode} • ${Formatters.formatDate(req.createdAt)}',
                                            style: AppTypography.bodySmall,
                                          ),
                                        ],
                                      ),
                                    ],
                                  ),
                                  StatusBadge(
                                    label: isCompleted
                                        ? 'Đã xong'
                                        : (isInProgress
                                            ? 'Đang sửa'
                                            : 'Chờ tiếp nhận'),
                                    tone: isCompleted
                                        ? StatusTone.green
                                        : (isInProgress
                                            ? StatusTone.amber
                                            : StatusTone.slate),
                                  ),
                                ],
                              ),
                              const SizedBox(height: 10),
                              Text(req.description,
                                  style: AppTypography.bodyMedium),
                              if (req.technicianName != null) ...[
                                const Divider(height: 20),
                                Row(
                                  mainAxisAlignment:
                                      MainAxisAlignment.spaceBetween,
                                  children: [
                                    Row(
                                      children: [
                                        const Icon(Icons.person_outline_rounded,
                                            size: 16,
                                            color: AppColors.textSecondary),
                                        const SizedBox(width: 6),
                                        Text('Kỹ thuật: ${req.technicianName}',
                                            style: AppTypography.bodySmall),
                                      ],
                                    ),
                                    if (req.technicianPhone != null)
                                      Text(req.technicianPhone!,
                                          style: AppTypography.labelSmall
                                              .copyWith(
                                                  color: AppColors.primary)),
                                  ],
                                ),
                              ],
                              if (isCompleted && req.actualCost != null) ...[
                                const SizedBox(height: 4),
                                Row(
                                  mainAxisAlignment:
                                      MainAxisAlignment.spaceBetween,
                                  children: [
                                    Text('Chi phí bảo trì / sửa chữa:',
                                        style: AppTypography.bodySmall),
                                    Text(
                                      req.actualCost == 0
                                          ? 'Miễn phí (chủ nhà đài thọ)'
                                          : Formatters.formatCurrency(
                                              req.actualCost!),
                                      style: AppTypography.labelMedium.copyWith(
                                        color: req.actualCost == 0
                                            ? AppColors.success
                                            : AppColors.primary,
                                      ),
                                    ),
                                  ],
                                ),
                              ],
                            ],
                          ),
                        );
                      },
                    ),
            ),
    );
  }
}
