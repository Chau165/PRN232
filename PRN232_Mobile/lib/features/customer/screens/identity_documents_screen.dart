import 'dart:io';
import 'package:flutter/material.dart';
import 'package:image_picker/image_picker.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/identity_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/app_text_field.dart';
import '../../../core/widgets/status_badge.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';

class IdentityDocumentsScreen extends StatefulWidget {
  const IdentityDocumentsScreen({super.key});

  @override
  State<IdentityDocumentsScreen> createState() => _IdentityDocumentsScreenState();
}

class _IdentityDocumentsScreenState extends State<IdentityDocumentsScreen> {
  final IdentityService _identityService = IdentityService();
  final ImagePicker _picker = ImagePicker();
  IdentityDocumentModel? _doc;

  final TextEditingController _nameController = TextEditingController();
  final TextEditingController _idNumberController = TextEditingController();
  XFile? _frontImage;
  XFile? _backImage;
  bool _isSubmitting = false;

  @override
  void initState() {
    super.initState();
    _doc = CustomerMockData.myIdentityDocument;
    if (_doc != null) {
      _nameController.text = _doc!.fullName;
      _idNumberController.text = _doc!.idNumber;
    }
    _loadDocument();
  }

  Future<void> _loadDocument() async {
    try {
      final docs = await _identityService.getMyDocuments();
      if (docs.isNotEmpty && mounted) {
        setState(() {
          _doc = docs.first;
          _idNumberController.text = _doc!.idNumber;
        });
      }
    } catch (_) {}
  }

  @override
  void dispose() {
    _nameController.dispose();
    _idNumberController.dispose();
    super.dispose();
  }

  Future<void> _pickImage(bool isFront, ImageSource source) async {
    final file = await _picker.pickImage(
      source: source,
      maxWidth: 1200,
      maxHeight: 1200,
      imageQuality: 85,
    );
    if (file != null) {
      setState(() {
        if (isFront) {
          _frontImage = file;
        } else {
          _backImage = file;
        }
      });
    }
  }

  void _submitDocument() async {
    if (_nameController.text.trim().isEmpty || _idNumberController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng nhập đầy đủ Họ tên và Số CCCD.')),
      );
      return;
    }

    if (_frontImage == null && _doc?.frontImageUrl == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng chụp ảnh mặt trước thẻ CCCD.')),
      );
      return;
    }

    setState(() => _isSubmitting = true);

    try {
      final doc = await _identityService.submitDocument(_idNumberController.text.trim());
      if (!mounted) return;
      setState(() {
        _doc = doc;
        _isSubmitting = false;
      });

      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Đã gửi hồ sơ định danh CCCD. Quản lý sẽ xác thực trong 24h.'),
          backgroundColor: AppColors.primary,
          behavior: SnackBarBehavior.floating,
        ),
      );
    } catch (e) {
      if (!mounted) return;
      setState(() => _isSubmitting = false);
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(content: Text(e.toString().replaceFirst('Exception: ', ''))),
      );
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Hồ Sơ Định Danh CCCD'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Status banner
            if (_doc != null) ...[
              AppCard(
                color: _doc!.isVerified
                    ? AppColors.successBg
                    : (_doc!.isPending ? AppColors.warningBg : AppColors.dangerBg),
                border: BorderSide(
                  color: _doc!.isVerified
                      ? const Color(0xFFA7F3D0)
                      : (_doc!.isPending ? const Color(0xFFFDE68A) : const Color(0xFFFECDD3)),
                ),
                child: Row(
                  children: [
                    Icon(
                      _doc!.isVerified
                          ? Icons.verified_user_rounded
                          : (_doc!.isPending ? Icons.pending_rounded : Icons.gpp_bad_rounded),
                      color: _doc!.isVerified
                          ? AppColors.success
                          : (_doc!.isPending ? AppColors.warning : AppColors.danger),
                      size: 28,
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _doc!.isVerified
                                ? 'Đã xác thực nhân thân thành công'
                                : (_doc!.isPending
                                    ? 'Hồ sơ đang chờ Quản lý kiểm duyệt'
                                    : 'Hồ sơ bị từ chối xác thực'),
                            style: AppTypography.labelLarge,
                          ),
                          Text(
                            _doc!.isVerified
                                ? 'Hồ sơ hợp lệ để làm thủ tục đăng ký tạm trú lưu trú'
                                : (_doc!.isPending
                                    ? 'Gửi lúc ${Formatters.formatDateTime(_doc!.uploadedAt)}'
                                    : 'Lý do: ${_doc!.rejectReason ?? "Ảnh mờ hoặc thông tin không trùng khớp"}'),
                            style: AppTypography.bodySmall,
                          ),
                        ],
                      ),
                    ),
                    StatusBadge(
                      label: _doc!.isVerified
                          ? 'Đã duyệt'
                          : (_doc!.isPending ? 'Chờ duyệt' : 'Từ chối'),
                      tone: _doc!.isVerified
                          ? StatusTone.green
                          : (_doc!.isPending ? StatusTone.amber : StatusTone.rose),
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 20),
            ],

            // Input Fields
            Text('Thông tin căn cước công dân', style: AppTypography.headingMedium),
            const SizedBox(height: 12),
            AppCard(
              child: Column(
                children: [
                  AppTextField(
                    label: 'Họ và tên (như trên thẻ)',
                    hint: 'NGUYEN VAN A',
                    controller: _nameController,
                    readOnly: _doc?.isVerified ?? false,
                  ),
                  const SizedBox(height: 14),
                  AppTextField(
                    label: 'Số CCCD (12 chữ số)',
                    hint: '07920100xxxx',
                    controller: _idNumberController,
                    keyboardType: TextInputType.number,
                    readOnly: _doc?.isVerified ?? false,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Front Image
            Text('Ảnh mặt trước thẻ CCCD', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            _buildPhotoPicker(
              isFront: true,
              localFile: _frontImage,
              networkUrl: _doc?.frontImageUrl,
              canEdit: !(_doc?.isVerified ?? false),
            ),
            const SizedBox(height: 20),

            // Back Image
            Text('Ảnh mặt sau thẻ CCCD (có chip)', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            _buildPhotoPicker(
              isFront: false,
              localFile: _backImage,
              networkUrl: _doc?.backImageUrl,
              canEdit: !(_doc?.isVerified ?? false),
            ),
            const SizedBox(height: 24),

            if (!(_doc?.isVerified ?? false))
              AppButton(
                text: _doc == null ? 'Gửi hồ sơ định danh' : 'Cập nhật lại hồ sơ',
                isLoading: _isSubmitting,
                onPressed: _submitDocument,
              ),
            const SizedBox(height: 20),
          ],
        ),
      ),
    );
  }

  Widget _buildPhotoPicker({
    required bool isFront,
    required XFile? localFile,
    required String? networkUrl,
    required bool canEdit,
  }) {
    if (localFile != null) {
      return Stack(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Image.file(
              File(localFile.path),
              height: 160,
              width: double.infinity,
              fit: BoxFit.cover,
            ),
          ),
          if (canEdit)
            Positioned(
              top: 8,
              right: 8,
              child: CircleAvatar(
                backgroundColor: Colors.black54,
                radius: 16,
                child: IconButton(
                  padding: EdgeInsets.zero,
                  icon: const Icon(Icons.close, color: Colors.white, size: 18),
                  onPressed: () {
                    setState(() {
                      if (isFront) {
                        _frontImage = null;
                      } else {
                        _backImage = null;
                      }
                    });
                  },
                ),
              ),
            ),
        ],
      );
    }

    if (networkUrl != null && networkUrl.isNotEmpty) {
      return Stack(
        children: [
          ClipRRect(
            borderRadius: BorderRadius.circular(12),
            child: Image.network(
              networkUrl,
              height: 160,
              width: double.infinity,
              fit: BoxFit.cover,
              errorBuilder: (_, _, _) => Container(
                height: 160,
                color: AppColors.surfaceVariant,
                alignment: Alignment.center,
                child: const Icon(Icons.badge_outlined, size: 48, color: AppColors.primary),
              ),
            ),
          ),
          if (canEdit)
            Positioned(
              bottom: 8,
              right: 8,
              child: ElevatedButton.icon(
                style: ElevatedButton.styleFrom(
                  backgroundColor: Colors.white,
                  foregroundColor: AppColors.primary,
                  elevation: 2,
                ),
                icon: const Icon(Icons.camera_alt_outlined, size: 16),
                label: const Text('Chụp lại'),
                onPressed: () => _pickImage(isFront, ImageSource.camera),
              ),
            ),
        ],
      );
    }

    return AppCard(
      padding: const EdgeInsets.symmetric(vertical: 24, horizontal: 16),
      child: Center(
        child: Column(
          children: [
            const Icon(Icons.add_a_photo_outlined, size: 40, color: AppColors.textMuted),
            const SizedBox(height: 10),
            Text('Tải ảnh rõ nét, không bị lóa sáng hay mất góc',
                style: AppTypography.bodySmall.copyWith(color: AppColors.textSecondary)),
            const SizedBox(height: 14),
            Row(
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                OutlinedButton.icon(
                  icon: const Icon(Icons.camera_alt_rounded, size: 16),
                  label: const Text('Chụp ảnh'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.primary,
                    side: const BorderSide(color: AppColors.primary),
                  ),
                  onPressed: () => _pickImage(isFront, ImageSource.camera),
                ),
                const SizedBox(width: 12),
                OutlinedButton.icon(
                  icon: const Icon(Icons.photo_library_rounded, size: 16),
                  label: const Text('Thư viện'),
                  style: OutlinedButton.styleFrom(
                    foregroundColor: AppColors.textSecondary,
                    side: const BorderSide(color: AppColors.border),
                  ),
                  onPressed: () => _pickImage(isFront, ImageSource.gallery),
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}
