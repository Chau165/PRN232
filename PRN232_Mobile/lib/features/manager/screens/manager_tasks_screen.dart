import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/services/maintenance_service.dart';
import '../../../core/services/viewing_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';
import '../widgets/assign_technician_sheet.dart';
import '../widgets/maintenance_report_sheet.dart';

class ManagerTasksScreen extends StatefulWidget {
  const ManagerTasksScreen({super.key});

  @override
  State<ManagerTasksScreen> createState() => _ManagerTasksScreenState();
}

class _ManagerTasksScreenState extends State<ManagerTasksScreen> {
  final ViewingService _viewingService = ViewingService();
  final BookingService _bookingService = BookingService();
  final MaintenanceService _maintenanceService = MaintenanceService();

  int _currentTab = 0; // 0: Hẹn xem, 1: Duyệt cọc, 2: Bảo trì
  List<ManagerTaskModel> _tasks = [];
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _tasks = List.from(ManagerMockData.tasks);
    _loadTasks();
  }

  Future<void> _loadTasks() async {
    setState(() => _isLoading = true);
    try {
      final viewings = await _viewingService.getManagerViewings();
      final bookings = await _bookingService.getBookings();
      final depositTasks = bookings.map((b) => ManagerTaskModel.fromBookingJson({
            'bookingId': b.id,
            'roomCode': b.roomCode,
            'propertyName': b.propertyName,
            'customerName': 'Khách thuê',
            'customerPhone': '',
            'depositAmount': b.depositAmount,
            'status': b.status,
          })).toList();
      final maintenance = await _maintenanceService.getManagerRequests();
      if (mounted) {
        setState(() {
          _tasks = [...viewings, ...depositTasks, ...maintenance];
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  List<ManagerTaskModel> get _viewingTasks =>
      _tasks.where((t) => t.type == TaskType.viewing).toList();

  List<ManagerTaskModel> get _depositTasks =>
      _tasks.where((t) => t.type == TaskType.deposit).toList();

  List<ManagerTaskModel> get _maintenanceTasks =>
      _tasks.where((t) => t.type == TaskType.maintenance).toList();

  void _approveViewing(ManagerTaskModel task) {
    setState(() => task.status = TaskStatus.approved);
    _viewingService.approveViewing(task.id);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã xác nhận lịch hẹn xem phòng với ${task.customerName}!'),
        backgroundColor: AppColors.primary,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _rejectViewing(ManagerTaskModel task) {
    setState(() => task.status = TaskStatus.rejected);
    _viewingService.rejectViewing(task.id);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã từ chối lịch xem phòng của ${task.customerName}.'),
        backgroundColor: AppColors.danger,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _approveDeposit(ManagerTaskModel task) {
    setState(() => task.status = TaskStatus.approved);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã xác nhận tiền cọc cho ${task.roomCode}!'),
        backgroundColor: AppColors.primary,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _rejectDeposit(ManagerTaskModel task) {
    setState(() => task.status = TaskStatus.rejected);
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã từ chối giao dịch cọc phòng ${task.roomCode}.'),
        backgroundColor: AppColors.danger,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  Future<void> _openAssignTechnician(ManagerTaskModel task) async {
    final tech = await AssignTechnicianSheet.show(context, task: task);
    if (tech != null) {
      setState(() {
        task.status = TaskStatus.inProgress;
        task.technicianName = tech.name;
      });
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Đã gán việc cho thợ ${tech.name} (${tech.phone})!'),
            backgroundColor: AppColors.primary,
            behavior: SnackBarBehavior.floating,
          ),
        );
      }
    }
  }

  void _completeMaintenance(ManagerTaskModel task) async {
    final success = await MaintenanceReportSheet.show(context, task: task);
    if (success == true) {
      setState(() {});
      if (mounted) {
        ScaffoldMessenger.of(context).showSnackBar(
          SnackBar(
            content: Text('Đã hoàn tất nghiệm thu & báo cáo chi phí sự cố tại ${task.roomCode}!'),
            backgroundColor: AppColors.success,
            behavior: SnackBarBehavior.floating,
          ),
        );
      }
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Duyệt Việc & Điều Phối'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: Column(
        children: [
          // Sub-Tab Switcher
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 8),
            color: Colors.white,
            child: Row(
              children: [
                _buildTabButton(0, 'Hẹn xem (${_viewingTasks.length})'),
                const SizedBox(width: 8),
                _buildTabButton(1, 'Duyệt cọc (${_depositTasks.length})'),
                const SizedBox(width: 8),
                _buildTabButton(2, 'Bảo trì (${_maintenanceTasks.length})'),
              ],
            ),
          ),
          if (_isLoading) const LinearProgressIndicator(minHeight: 2),
          const Divider(height: 1),

          Expanded(
            child: ListView(
              padding: const EdgeInsets.all(16),
              children: [
                if (_currentTab == 0) ...[
                  ..._viewingTasks.map((t) => Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: _buildViewingTaskCard(t),
                      )),
                ] else if (_currentTab == 1) ...[
                  ..._depositTasks.map((t) => Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: _buildDepositTaskCard(t),
                      )),
                ] else ...[
                  ..._maintenanceTasks.map((t) => Padding(
                        padding: const EdgeInsets.only(bottom: 12),
                        child: _buildMaintenanceTaskCard(t),
                      )),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _buildTabButton(int index, String label) {
    final isSelected = _currentTab == index;
    return Expanded(
      child: GestureDetector(
        onTap: () => setState(() => _currentTab = index),
        child: Container(
          padding: const EdgeInsets.symmetric(vertical: 9),
          decoration: BoxDecoration(
            color: isSelected ? AppColors.primary : AppColors.surfaceVariant,
            borderRadius: BorderRadius.circular(10),
          ),
          alignment: Alignment.center,
          child: Text(
            label,
            style: AppTypography.labelSmall.copyWith(
              color: isSelected ? Colors.white : AppColors.textSecondary,
              fontWeight: isSelected ? FontWeight.bold : FontWeight.normal,
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildViewingTaskCard(ManagerTaskModel task) {
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
                  Text(task.customerName, style: AppTypography.headingSmall),
                ],
              ),
              StatusBadge(
                label: task.isPending
                    ? 'Chờ duyệt'
                    : (task.isApproved ? 'Đã duyệt' : 'Đã từ chối'),
                tone: task.isPending
                    ? StatusTone.amber
                    : (task.isApproved ? StatusTone.green : StatusTone.rose),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text('SĐT: ${task.customerPhone}', style: AppTypography.bodySmall),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: AppColors.primaryLight,
              borderRadius: BorderRadius.circular(8),
            ),
            child: Row(
              children: [
                const Icon(Icons.access_time_rounded,
                    size: 16, color: AppColors.primary),
                const SizedBox(width: 6),
                Text(
                  task.targetDate != null
                      ? Formatters.formatDateTime(task.targetDate!)
                      : 'Hôm nay lúc 16:30',
                  style: AppTypography.labelMedium
                      .copyWith(color: AppColors.primary),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
          Text('Ghi chú: ${task.description}', style: AppTypography.bodySmall),
          if (task.isPending) ...[
            const Divider(height: 20),
            Row(
              children: [
                Expanded(
                  child: AppButton(
                    text: 'Từ chối',
                    height: 38,
                    variant: AppButtonVariant.outline,
                    onPressed: () => _rejectViewing(task),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: AppButton(
                    text: 'Duyệt lịch hẹn',
                    height: 38,
                    onPressed: () => _approveViewing(task),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildDepositTaskCard(ManagerTaskModel task) {
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
                  Text(task.customerName, style: AppTypography.headingSmall),
                ],
              ),
              StatusBadge(
                label: task.isPending
                    ? 'Chờ xác nhận'
                    : (task.isApproved ? 'Đã xác nhận cọc' : 'Đã hủy cọc'),
                tone: task.isPending
                    ? StatusTone.teal
                    : (task.isApproved ? StatusTone.green : StatusTone.rose),
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text('SĐT: ${task.customerPhone}', style: AppTypography.bodySmall),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Số tiền cọc:', style: AppTypography.bodySmall),
              Text(
                Formatters.formatCurrency(task.amount ?? 3500000),
                style: AppTypography.headingLarge
                    .copyWith(color: AppColors.primary),
              ),
            ],
          ),
          const SizedBox(height: 4),
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              Text('Phương thức:', style: AppTypography.bodySmall),
              Text(
                '${task.paymentMethod ?? "VNPay"} • Vừa nhận',
                style: AppTypography.labelMedium,
              ),
            ],
          ),
          if (task.isPending) ...[
            const Divider(height: 20),
            Row(
              children: [
                Expanded(
                  child: AppButton(
                    text: 'Hủy cọc',
                    height: 38,
                    variant: AppButtonVariant.outline,
                    onPressed: () => _rejectDeposit(task),
                  ),
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: AppButton(
                    text: 'Xác nhận cọc',
                    height: 38,
                    onPressed: () => _approveDeposit(task),
                  ),
                ),
              ],
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildMaintenanceTaskCard(ManagerTaskModel task) {
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
                  Text(task.title, style: AppTypography.labelLarge),
                ],
              ),
              StatusBadge(
                label: task.priority ?? 'Bình thường',
                tone: task.priority == 'Khẩn cấp'
                    ? StatusTone.rose
                    : StatusTone.amber,
              ),
            ],
          ),
          const SizedBox(height: 6),
          Text(task.description, style: AppTypography.bodySmall),
          const SizedBox(height: 4),
          Text(
            'Khách báo: ${task.customerName} (${task.customerPhone})',
            style: AppTypography.bodySmall
                .copyWith(color: AppColors.textSecondary),
          ),
          if (task.technicianName != null) ...[
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: AppColors.successBg,
                borderRadius: BorderRadius.circular(6),
              ),
              child: Row(
                children: [
                  const Icon(Icons.handyman_rounded,
                      size: 16, color: AppColors.success),
                  const SizedBox(width: 6),
                  Text('Thợ phụ trách: ${task.technicianName}',
                      style: AppTypography.bodySmall
                          .copyWith(color: AppColors.successText, fontWeight: FontWeight.bold)),
                ],
              ),
            ),
          ],
          const Divider(height: 20),
          Row(
            children: [
              if (task.isCompleted) ...[
                const Expanded(
                  child: Center(
                    child: Text(
                      '✓ Sự cố đã được xử lý xong',
                      style: TextStyle(
                          color: AppColors.success,
                          fontWeight: FontWeight.bold),
                    ),
                  ),
                ),
              ] else if (task.isInProgress) ...[
                Expanded(
                  child: AppButton(
                    text: 'Đã hoàn tất sửa chữa',
                    height: 38,
                    onPressed: () => _completeMaintenance(task),
                  ),
                ),
              ] else ...[
                Expanded(
                  child: AppButton(
                    text: 'Gán thợ kỹ thuật',
                    height: 38,
                    onPressed: () => _openAssignTechnician(task),
                  ),
                ),
              ],
            ],
          ),
        ],
      ),
    );
  }
}
