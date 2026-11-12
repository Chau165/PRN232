import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/widgets/app_bottom_sheet.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';

class AssignTechnicianSheet extends StatefulWidget {
  final ManagerTaskModel task;

  const AssignTechnicianSheet({
    super.key,
    required this.task,
  });

  static Future<TechnicianModel?> show(
    BuildContext context, {
    required ManagerTaskModel task,
  }) {
    return AppBottomSheet.show<TechnicianModel>(
      context: context,
      title: 'Điều phối thợ sửa chữa',
      subtitle: '${task.roomCode} • ${task.title}',
      child: AssignTechnicianSheet(task: task),
    );
  }

  @override
  State<AssignTechnicianSheet> createState() => _AssignTechnicianSheetState();
}

class _AssignTechnicianSheetState extends State<AssignTechnicianSheet> {
  TechnicianModel? _selectedTechnician;
  final TextEditingController _noteController = TextEditingController();

  @override
  void initState() {
    super.initState();
    // Default to first available technician
    try {
      _selectedTechnician = ManagerMockData.technicians
          .firstWhere((t) => t.isAvailable);
    } catch (_) {
      if (ManagerMockData.technicians.isNotEmpty) {
        _selectedTechnician = ManagerMockData.technicians.first;
      }
    }
  }

  @override
  void dispose() {
    _noteController.dispose();
    super.dispose();
  }

  void _confirmAssign() {
    if (_selectedTechnician == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng chọn một thợ sửa chữa.')),
      );
      return;
    }
    Navigator.pop(context, _selectedTechnician);
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Problem summary
        AppCard(
          color: AppColors.primaryLight,
          border: const BorderSide(color: Color(0xFFC7E7E2)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(widget.task.roomCode,
                      style: AppTypography.headingSmall
                          .copyWith(color: AppColors.primary)),
                  StatusBadge(
                    label: widget.task.priority ?? 'Bình thường',
                    tone: widget.task.priority == 'Khẩn cấp'
                        ? StatusTone.rose
                        : StatusTone.amber,
                  ),
                ],
              ),
              const SizedBox(height: 4),
              Text(widget.task.title, style: AppTypography.labelLarge),
              Text(widget.task.description, style: AppTypography.bodySmall),
              const SizedBox(height: 4),
              Text(
                'Khách: ${widget.task.customerName} (${widget.task.customerPhone})',
                style: AppTypography.bodySmall
                    .copyWith(fontWeight: FontWeight.w600),
              ),
            ],
          ),
        ),
        const SizedBox(height: 18),

        Text('Chọn thợ kỹ thuật phụ trách',
            style: AppTypography.headingSmall),
        const SizedBox(height: 10),

        ListView.separated(
          shrinkWrap: true,
          physics: const NeverScrollableScrollPhysics(),
          itemCount: ManagerMockData.technicians.length,
          separatorBuilder: (context, index) => const SizedBox(height: 10),
          itemBuilder: (context, index) {
            final tech = ManagerMockData.technicians[index];
            final isSelected = _selectedTechnician?.id == tech.id;

            return AppCard(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
              border: BorderSide(
                color: isSelected ? AppColors.primary : AppColors.border,
                width: isSelected ? 1.5 : 1,
              ),
              color: isSelected
                  ? AppColors.primaryLight.withValues(alpha: 0.4)
                  : Colors.white,
              onTap: () {
                setState(() => _selectedTechnician = tech);
              },
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: isSelected
                          ? AppColors.primary
                          : AppColors.surfaceVariant,
                      shape: BoxShape.circle,
                    ),
                    child: Icon(
                      Icons.person_rounded,
                      color: isSelected ? Colors.white : AppColors.primary,
                      size: 20,
                    ),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Row(
                          children: [
                            Text(tech.name, style: AppTypography.labelLarge),
                            const SizedBox(width: 6),
                            const Icon(Icons.star_rounded,
                                color: Color(0xFFF59E0B), size: 16),
                            Text('${tech.rating}',
                                style: AppTypography.bodySmall
                                    .copyWith(fontWeight: FontWeight.bold)),
                          ],
                        ),
                        const SizedBox(height: 2),
                        Text(
                          '${tech.specialty} • ${tech.phone}',
                          style: AppTypography.bodySmall
                              .copyWith(color: AppColors.textSecondary),
                        ),
                      ],
                    ),
                  ),
                  StatusBadge(
                    label: tech.isAvailable ? 'Sẵn sàng' : 'Đang bận',
                    tone: tech.isAvailable ? StatusTone.green : StatusTone.slate,
                  ),
                ],
              ),
            );
          },
        ),
        const SizedBox(height: 24),

        AppButton(
          text: _selectedTechnician != null
              ? 'Xác nhận gán việc cho ${_selectedTechnician!.name}'
              : 'Chọn thợ sửa chữa',
          onPressed: _confirmAssign,
        ),
        const SizedBox(height: 10),
      ],
    );
  }
}
