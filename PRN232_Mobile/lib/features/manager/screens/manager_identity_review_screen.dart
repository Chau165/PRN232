import 'package:flutter/material.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/identity_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';

class ManagerIdentityReviewScreen extends StatefulWidget {
  const ManagerIdentityReviewScreen({super.key});

  @override
  State<ManagerIdentityReviewScreen> createState() =>
      _ManagerIdentityReviewScreenState();
}

class _ManagerIdentityReviewScreenState
    extends State<ManagerIdentityReviewScreen> {
  final IdentityService _identityService = IdentityService();
  List<ManagerIdentityReviewModel> _reviews = [];
  bool _isLoading = false;

  @override
  void initState() {
    super.initState();
    _reviews = AuthState.isCurrentDemo ? List.from(ManagerMockData.identityReviews) : [];
    _loadReviews();
  }

  Future<void> _loadReviews() async {
    setState(() => _isLoading = true);
    try {
      final list = await _identityService.getManagerReviews();
      if (mounted) {
        setState(() {
          _reviews = list;
          _isLoading = false;
        });
      }
    } catch (_) {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _approve(ManagerIdentityReviewModel item) async {
    setState(() {
      item.status = 'Verified';
      item.rejectReason = null;
    });
    await _identityService.reviewDocument(item.id, 'Approved');
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã phê duyệt hồ sơ CCCD của khách ${item.tenantName}!'),
        backgroundColor: AppColors.primary,
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  void _reject(ManagerIdentityReviewModel item) {
    final reasonController = TextEditingController();

    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Từ chối hồ sơ định danh'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text('Nhập lý do từ chối gửi tới khách ${item.tenantName}:'),
            const SizedBox(height: 10),
            TextField(
              controller: reasonController,
              decoration: const InputDecoration(
                hintText: 'Ví dụ: Ảnh chụp bị lóa sáng, mờ số CCCD...',
                border: OutlineInputBorder(),
              ),
              maxLines: 2,
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            style: ElevatedButton.styleFrom(backgroundColor: AppColors.danger),
            onPressed: () async {
              final reason = reasonController.text.trim();
              setState(() {
                item.status = 'Rejected';
                item.rejectReason = reason.isNotEmpty ? reason : 'Ảnh không hợp lệ';
              });
              Navigator.pop(ctx);
              await _identityService.reviewDocument(item.id, 'Rejected');
              if (!mounted) return;
              ScaffoldMessenger.of(context).showSnackBar(
                SnackBar(
                  content: Text('Đã từ chối hồ sơ CCCD của ${item.tenantName}.'),
                  backgroundColor: AppColors.danger,
                  behavior: SnackBarBehavior.floating,
                ),
              );
            },
            child: const Text('Xác nhận từ chối', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );
  }

  void _viewFullImages(ManagerIdentityReviewModel item) {
    showDialog(
      context: context,
      builder: (ctx) => Dialog(
        insetPadding: const EdgeInsets.all(16),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text('${item.tenantName} • CCCD: ${item.idNumber}',
                  style: AppTypography.headingSmall),
              const SizedBox(height: 12),
              Text('Mặt trước:', style: AppTypography.bodySmall),
              const SizedBox(height: 4),
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Image.network(
                  item.frontImageUrl,
                  height: 160,
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => Container(
                    height: 160,
                    color: AppColors.surfaceVariant,
                    child: const Icon(Icons.badge_outlined, size: 40),
                  ),
                ),
              ),
              const SizedBox(height: 12),
              Text('Mặt sau:', style: AppTypography.bodySmall),
              const SizedBox(height: 4),
              ClipRRect(
                borderRadius: BorderRadius.circular(8),
                child: Image.network(
                  item.backImageUrl,
                  height: 160,
                  fit: BoxFit.cover,
                  errorBuilder: (_, _, _) => Container(
                    height: 160,
                    color: AppColors.surfaceVariant,
                    child: const Icon(Icons.badge_outlined, size: 40),
                  ),
                ),
              ),
              const SizedBox(height: 16),
              AppButton(
                text: 'Đóng',
                variant: AppButtonVariant.outline,
                onPressed: () => Navigator.pop(ctx),
              ),
            ],
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Kiểm Duyệt CCCD / Lưu Trú'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: Column(
        children: [
          if (_isLoading) const LinearProgressIndicator(minHeight: 2),
          Expanded(
            child: _reviews.isEmpty
                ? Center(
                    child: Column(
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        const Icon(Icons.verified_user_outlined, size: 48, color: AppColors.textMuted),
                        const SizedBox(height: 12),
                        Text('Không có hồ sơ nào cần kiểm duyệt',
                            style: AppTypography.bodyMedium.copyWith(color: AppColors.textSecondary)),
                      ],
                    ),
                  )
                : ListView.separated(
                    padding: const EdgeInsets.all(16),
                    itemCount: _reviews.length,
                    separatorBuilder: (_, _) => const SizedBox(height: 14),
                    itemBuilder: (context, index) {
                final item = _reviews[index];
                StatusTone tone = item.isVerified
                    ? StatusTone.green
                    : (item.isPending ? StatusTone.amber : StatusTone.rose);

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
                                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                                decoration: BoxDecoration(
                                  color: AppColors.primaryLight,
                                  borderRadius: BorderRadius.circular(6),
                                ),
                                child: Text(item.roomCode,
                                    style: AppTypography.labelSmall.copyWith(color: AppColors.primary)),
                              ),
                              const SizedBox(width: 8),
                              Text(item.tenantName, style: AppTypography.headingSmall),
                            ],
                          ),
                          StatusBadge(
                            label: item.isVerified
                                ? 'Đã duyệt'
                                : (item.isPending ? 'Chờ duyệt' : 'Từ chối'),
                            tone: tone,
                          ),
                        ],
                      ),
                      const SizedBox(height: 6),
                      Text('SĐT: ${item.tenantPhone} • Số CCCD: ${item.idNumber}',
                          style: AppTypography.bodySmall),
                      Text('Tải lên lúc: ${Formatters.formatDateTime(item.uploadedAt)}',
                          style: AppTypography.bodySmall.copyWith(color: AppColors.textMuted, fontSize: 11)),
                      const SizedBox(height: 12),
                      Row(
                        children: [
                          Expanded(
                            child: OutlinedButton.icon(
                              icon: const Icon(Icons.visibility_outlined, size: 16),
                              label: const Text('Xem ảnh CCCD 2 mặt'),
                              style: OutlinedButton.styleFrom(
                                foregroundColor: AppColors.primary,
                                side: const BorderSide(color: AppColors.primary),
                              ),
                              onPressed: () => _viewFullImages(item),
                            ),
                          ),
                        ],
                      ),
                      if (item.isPending) ...[
                        const Divider(height: 20),
                        Row(
                          children: [
                            Expanded(
                              child: OutlinedButton(
                                style: OutlinedButton.styleFrom(
                                  foregroundColor: AppColors.danger,
                                  side: const BorderSide(color: AppColors.danger),
                                ),
                                onPressed: () => _reject(item),
                                child: const Text('Từ chối'),
                              ),
                            ),
                            const SizedBox(width: 12),
                            Expanded(
                              child: ElevatedButton(
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: AppColors.primary,
                                ),
                                onPressed: () => _approve(item),
                                child: const Text('Phê duyệt CCCD', style: TextStyle(color: Colors.white)),
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
        ],
      ),
    );
  }
}
