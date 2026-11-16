import 'dart:io';
import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import 'package:image_picker/image_picker.dart';
import 'package:url_launcher/url_launcher.dart';
import '../../../core/auth/auth_state.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_application_service.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../routes/app_routes.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';
import '../widgets/payos_qr_dialog.dart';

class DepositScreen extends StatefulWidget {
  final PropertyItem property;
  final RoomItem room;
  final CustomerBookingModel? initialBooking;

  const DepositScreen({
    super.key,
    required this.property,
    required this.room,
    this.initialBooking,
  });

  @override
  State<DepositScreen> createState() => _DepositScreenState();
}

class _DepositScreenState extends State<DepositScreen> {
  final BookingService _bookingService = BookingService();
  final BookingApplicationService _bookingAppService = BookingApplicationService();

  // Step 1: Chuẩn bị hồ sơ CCCD & Đặt cọc, Step 2: Thanh toán payOS
  int _currentStep = 1;
  CustomerBookingModel? _createdBooking;

  // Form inputs for Step 1
  late DateTime _moveInDate;
  int _occupantCount = 1;
  late final TextEditingController _occupantController;
  String? _dateError;
  String? _occupantError;
  String? _generalError;

  // Hồ sơ người thuê CCCD
  BookingApplicationModel? _application;
  bool _isAppLoading = false;
  bool _isSubmittingApp = false;
  String? _appError;

  // Thông tin CCCD người đứng tên dự kiến (position 0)
  late final TextEditingController _nameController;
  late final TextEditingController _idNumberController;
  bool _consentConfirmed = false;
  bool _isSavingPerson = false;
  bool _isUploadingFront = false;
  bool _isUploadingBack = false;
  String? _frontLocalPath;
  String? _backLocalPath;
  String? _personError;
  bool _personDirty = false;

  @override
  void initState() {
    super.initState();
    _moveInDate = DateTime.now().add(const Duration(days: 4));
    _occupantController = TextEditingController(text: _occupantCount.toString());

    final user = AuthState.instance.currentUser;
    final initialName = user?.fullName.isNotEmpty == true ? user!.fullName : 'BabyBoo';
    _nameController = TextEditingController(text: initialName);
    _idNumberController = TextEditingController();

    if (widget.initialBooking != null) {
      _createdBooking = widget.initialBooking;
      _currentStep = 2;
    }
  }

  @override
  void dispose() {
    _occupantController.dispose();
    _nameController.dispose();
    _idNumberController.dispose();
    super.dispose();
  }

  bool get _isMismatch {
    if (_application == null) return false;
    final appDateStr = Formatters.formatDate(_application!.moveInDate);
    final curDateStr = Formatters.formatDate(_moveInDate);
    return appDateStr != curDateStr || _application!.occupantCount != _occupantCount;
  }

  bool get _isReadyToSubmit {
    if (_application == null || _isMismatch || _isAppLoading) return false;
    final holder = _application!.mainTenant;
    if (holder == null) return false;
    final hasName = holder.fullName.trim().isNotEmpty;
    final hasId = (holder.maskedIdentityNumber != null && holder.maskedIdentityNumber!.isNotEmpty) ||
        _idNumberController.text.length == 12;
    return hasName && hasId && holder.consentConfirmed && holder.hasFront && holder.hasBack && !_personDirty;
  }

  void _syncApplicationState(BookingApplicationModel app) {
    setState(() {
      _application = app;
      final holder = app.mainTenant;
      if (holder != null) {
        if (!_personDirty && holder.fullName.isNotEmpty) {
          _nameController.text = holder.fullName;
        }
        _consentConfirmed = holder.consentConfirmed;
      }
    });
  }

  Future<void> _prepareOrUpdateApplication() async {
    final now = DateTime.now();
    final today = DateTime(now.year, now.month, now.day);
    final pickedDateOnly = DateTime(_moveInDate.year, _moveInDate.month, _moveInDate.day);

    setState(() {
      _dateError = null;
      _occupantError = null;
      _appError = null;
    });

    if (pickedDateOnly.isBefore(today)) {
      setState(() => _dateError = 'Ngày dọn vào không được ở quá khứ.');
      return;
    } else if (pickedDateOnly.difference(today).inDays > 14) {
      setState(() => _dateError = 'Ngày dọn vào tối đa 14 ngày kể từ hôm nay.');
      return;
    }

    final maxOccupancy = widget.room.maxOccupancy > 0 ? widget.room.maxOccupancy : 2;
    if (_occupantCount < 1) {
      setState(() => _occupantError = 'Số người ở tối thiểu là 1.');
      return;
    } else if (_occupantCount > maxOccupancy) {
      setState(() =>
          _occupantError = 'Phòng ${widget.room.roomCode} chỉ được ở tối đa $maxOccupancy người.');
      return;
    }

    setState(() => _isAppLoading = true);

    try {
      BookingApplicationModel app;
      if (_application == null) {
        app = await _bookingAppService.prepareApplication(
          roomId: widget.room.roomId,
          moveInDate: _moveInDate,
          occupantCount: _occupantCount,
        );
      } else {
        app = await _bookingAppService.updateApplication(
          applicationId: _application!.bookingApplicationId,
          moveInDate: _moveInDate,
          occupantCount: _occupantCount,
          version: _application!.version,
          confirmRemovePeople: true,
        );
      }

      if (!mounted) return;
      _syncApplicationState(app);
      setState(() {
        _isAppLoading = false;
        _personDirty = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isAppLoading = false;
        _appError = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  Future<void> _reloadApplication() async {
    if (_application == null) {
      await _prepareOrUpdateApplication();
      return;
    }
    setState(() {
      _isAppLoading = true;
      _appError = null;
    });
    try {
      final app = await _bookingAppService.loadApplication(_application!.bookingApplicationId);
      if (!mounted) return;
      _syncApplicationState(app);
      setState(() {
        _isAppLoading = false;
        _personDirty = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isAppLoading = false;
        _appError = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  Future<bool> _savePersonInfo() async {
    if (_application == null) return false;
    final holder = _application!.mainTenant;
    if (holder == null) return false;

    final name = _nameController.text.trim();
    final idNumber = _idNumberController.text.trim();

    setState(() => _personError = null);

    if (name.isEmpty) {
      setState(() => _personError = 'Vui lòng nhập họ và tên của bạn.');
      return false;
    }

    if (idNumber.isEmpty && (holder.maskedIdentityNumber == null || holder.maskedIdentityNumber!.isEmpty)) {
      setState(() => _personError = 'Vui lòng nhập số CCCD 12 chữ số.');
      return false;
    }

    if (idNumber.isNotEmpty && idNumber.length != 12) {
      setState(() => _personError = 'Số CCCD phải gồm đúng 12 chữ số.');
      return false;
    }

    if (!_consentConfirmed) {
      setState(() => _personError = 'Vui lòng tích ô đồng ý cung cấp thông tin và ảnh CCCD.');
      return false;
    }

    setState(() => _isSavingPerson = true);
    try {
      final updatedApp = await _bookingAppService.savePerson(
        applicationId: _application!.bookingApplicationId,
        personId: holder.personId,
        fullName: name,
        identityNumber: idNumber.isNotEmpty ? idNumber : null,
        consentConfirmed: _consentConfirmed,
        version: _application!.version,
      );

      if (!mounted) return false;
      _syncApplicationState(updatedApp);
      setState(() {
        _isSavingPerson = false;
        _personDirty = false;
        _idNumberController.clear();
      });
      return true;
    } catch (e) {
      if (!mounted) return false;
      setState(() {
        _isSavingPerson = false;
        _personError = e.toString().replaceFirst('Exception: ', '');
      });
      return false;
    }
  }

  Future<void> _pickAndUploadImage(String side) async {
    if (!_consentConfirmed) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(
          content: Text('Vui lòng tích chọn đồng ý cung cấp thông tin CCCD trước khi tải ảnh.'),
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }

    if (_personDirty) {
      final saved = await _savePersonInfo();
      if (!saved) return;
    }

    if (_application == null) return;
    final holder = _application!.mainTenant;
    if (holder == null) return;

    final ImagePicker picker = ImagePicker();
    final source = await showModalBottomSheet<ImageSource>(
      context: context,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(16)),
      ),
      builder: (ctx) => SafeArea(
        child: Padding(
          padding: const EdgeInsets.symmetric(vertical: 16),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                'Chọn ảnh CCCD ($side)',
                style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 16),
              ),
              const SizedBox(height: 12),
              ListTile(
                leading: const Icon(Icons.camera_alt, color: Color(0xFF087775)),
                title: const Text('Chụp ảnh mới bằng máy ảnh'),
                onTap: () => Navigator.pop(ctx, ImageSource.camera),
              ),
              ListTile(
                leading: const Icon(Icons.photo_library, color: Color(0xFF087775)),
                title: const Text('Chọn ảnh từ thư viện thiết bị'),
                onTap: () => Navigator.pop(ctx, ImageSource.gallery),
              ),
            ],
          ),
        ),
      ),
    );

    if (source == null) return;

    try {
      final XFile? file = await picker.pickImage(
        source: source,
        imageQuality: 85,
        maxWidth: 1600,
        maxHeight: 1600,
      );

      if (file == null) return;

      setState(() {
        if (side == 'Front') {
          _isUploadingFront = true;
          _frontLocalPath = file.path;
        } else {
          _isUploadingBack = true;
          _backLocalPath = file.path;
        }
      });

      final updatedApp = await _bookingAppService.uploadImage(
        applicationId: _application!.bookingApplicationId,
        personId: holder.personId,
        side: side,
        filePath: file.path,
        version: _application!.version,
      );

      if (!mounted) return;
      _syncApplicationState(updatedApp);
      setState(() {
        if (side == 'Front') _isUploadingFront = false;
        if (side == 'Back') _isUploadingBack = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        if (side == 'Front') _isUploadingFront = false;
        if (side == 'Back') _isUploadingBack = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Tải ảnh thất bại: ${e.toString().replaceFirst('Exception: ', '')}'),
          backgroundColor: const Color(0xFFE11D48),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  Future<void> _deleteImage(String side) async {
    if (_application == null) return;
    final holder = _application!.mainTenant;
    if (holder == null) return;

    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text('Xác nhận xóa ảnh CCCD ($side)?'),
        content: const Text('Bạn có chắc muốn xóa ảnh này không? Bạn sẽ cần tải lại trước khi đặt cọc.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('Hủy'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            style: ElevatedButton.styleFrom(backgroundColor: const Color(0xFFE11D48)),
            child: const Text('Xóa', style: TextStyle(color: Colors.white)),
          ),
        ],
      ),
    );

    if (confirm != true) return;

    setState(() {
      if (side == 'Front') _isUploadingFront = true;
      if (side == 'Back') _isUploadingBack = true;
    });

    try {
      final updatedApp = await _bookingAppService.deleteImage(
        applicationId: _application!.bookingApplicationId,
        personId: holder.personId,
        side: side,
        version: _application!.version,
      );

      if (!mounted) return;
      _syncApplicationState(updatedApp);
      setState(() {
        if (side == 'Front') {
          _isUploadingFront = false;
          _frontLocalPath = null;
        } else {
          _isUploadingBack = false;
          _backLocalPath = null;
        }
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        if (side == 'Front') _isUploadingFront = false;
        if (side == 'Back') _isUploadingBack = false;
      });
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text('Xóa ảnh thất bại: ${e.toString().replaceFirst('Exception: ', '')}'),
          backgroundColor: const Color(0xFFE11D48),
          behavior: SnackBarBehavior.floating,
        ),
      );
    }
  }

  void _previewImage(String side) async {
    final path = side == 'Front' ? _frontLocalPath : _backLocalPath;
    if (path != null && File(path).existsSync()) {
      showDialog(
        context: context,
        builder: (ctx) => Dialog(
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              ClipRRect(
                borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
                child: Image.file(File(path), fit: BoxFit.cover),
              ),
              Padding(
                padding: const EdgeInsets.all(8.0),
                child: TextButton(
                  onPressed: () => Navigator.pop(ctx),
                  child: const Text('Đóng ảnh'),
                ),
              ),
            ],
          ),
        ),
      );
      return;
    }

    if (_application != null) {
      final person = _application!.mainTenant;
      if (person != null) {
        final url = await _bookingAppService.readImageUrl(
          applicationId: _application!.bookingApplicationId,
          personId: person.personId,
          side: side,
        );
        if (url.isNotEmpty && mounted) {
          showDialog(
            context: context,
            builder: (ctx) => Dialog(
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  ClipRRect(
                    borderRadius: const BorderRadius.vertical(top: Radius.circular(16)),
                    child: Image.network(url, fit: BoxFit.cover),
                  ),
                  Padding(
                    padding: const EdgeInsets.all(8.0),
                    child: TextButton(
                      onPressed: () => Navigator.pop(ctx),
                      child: const Text('Đóng ảnh'),
                    ),
                  ),
                ],
              ),
            ),
          );
          return;
        }
      }
    }

    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(
        content: Text('Ảnh CCCD đã được lưu trữ bảo mật trên server.'),
        behavior: SnackBarBehavior.floating,
      ),
    );
  }

  Future<void> _submitApplicationAndProceed() async {
    if (!_isReadyToSubmit || _application == null) return;

    setState(() {
      _isSubmittingApp = true;
      _generalError = null;
    });

    try {
      final booking = await _bookingAppService.submitApplication(
        applicationId: _application!.bookingApplicationId,
        version: _application!.version,
      );

      if (!mounted) return;
      setState(() {
        _isSubmittingApp = false;
        _createdBooking = booking;
        _currentStep = 2;
      });

      // Mở dialog PayOS QR thanh toán ngay sau khi tạo booking thành công
      _openPayOS();
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isSubmittingApp = false;
        _generalError = e.toString().replaceFirst('Exception: ', '');
      });
    }
  }

  void _openPayOS() async {
    final booking = _createdBooking;
    if (booking == null) return;

    showDialog(
      context: context,
      barrierDismissible: false,
      builder: (_) => const Center(
        child: CircularProgressIndicator(color: Color(0xFF087775)),
      ),
    );

    BookingPaymentLinkModel? link;
    String? payError;
    try {
      link = await _bookingService.createPaymentLink(booking.id);
    } catch (e) {
      payError = e.toString().replaceFirst('Exception: ', '');
    }

    if (!mounted) return;
    Navigator.of(context).pop();

    if (link == null || link.checkoutUrl == null || link.checkoutUrl!.isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        SnackBar(
          content: Text(payError ?? 'Không thể tạo liên kết thanh toán payOS.'),
          backgroundColor: const Color(0xFFE11D48),
          behavior: SnackBarBehavior.floating,
        ),
      );
      return;
    }

    // 1. Mở trang checkout payOS thật với mã VietQR PRO thật
    final checkoutUri = Uri.parse(link.checkoutUrl!);
    try {
      final launched = await launchUrl(
        checkoutUri,
        mode: LaunchMode.inAppBrowserView,
      );
      if (!launched) {
        await launchUrl(checkoutUri, mode: LaunchMode.externalApplication);
      }
    } catch (_) {
      try {
        await launchUrl(checkoutUri, mode: LaunchMode.externalApplication);
      } catch (_) {}
    }

    if (!mounted) return;

    // 2. Chuyển sang màn hình "Đang chờ xác nhận tiền cọc" (giống ảnh Web)
    context.pushReplacement(
      AppRoutes.paymentResult,
      extra: {
        'requestId': link.paymentRequestId,
        'bookingId': booking.id,
        'amount': booking.depositAmount,
        'checkoutUrl': link.checkoutUrl,
        'isBooking': true,
      },
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFFF8FAFC),
      appBar: AppBar(
        title: const Text('Đặt cọc giữ phòng'),
        backgroundColor: Colors.white,
        foregroundColor: AppColors.textPrimary,
        elevation: 0,
        centerTitle: true,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Sub-header & Title
            Center(
              child: Column(
                children: const [
                  Text(
                    'KHU VỰC CỦA BẠN',
                    style: TextStyle(
                      color: Color(0xFF087775),
                      fontWeight: FontWeight.w700,
                      fontSize: 12,
                      letterSpacing: 1.2,
                    ),
                  ),
                  SizedBox(height: 6),
                  Text(
                    'Đặt cọc giữ phòng',
                    style: TextStyle(
                      color: Color(0xFF0F172A),
                      fontWeight: FontWeight.w800,
                      fontSize: 24,
                    ),
                  ),
                  SizedBox(height: 6),
                  Text(
                    'Tiền cọc tương đương 1 tháng tiền thuê.',
                    style: TextStyle(
                      color: Color(0xFF64748B),
                      fontSize: 14,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Card content
            Container(
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(16),
                boxShadow: [
                  BoxShadow(
                    color: Colors.black.withOpacity(0.04),
                    blurRadius: 16,
                    offset: const Offset(0, 4),
                  ),
                ],
              ),
              padding: const EdgeInsets.all(20),
              child: _currentStep == 1 ? _buildStep1HoldRoom() : _buildStep2PayOS(),
            ),
          ],
        ),
      ),
    );
  }

  /// Bước 1: Chọn ngày, số người, Upload CCCD và Tạo Booking (Hình 1 & 2)
  Widget _buildStep1HoldRoom() {
    final user = AuthState.instance.currentUser;
    final userName = user?.fullName.isNotEmpty == true ? user!.fullName : 'BabyBoo';
    final userPhone = user?.phoneNumber.isNotEmpty == true ? user!.phoneNumber : '0123456789';
    final maxOccupancy = widget.room.maxOccupancy > 0 ? widget.room.maxOccupancy : 2;

    final holdEndDate = _moveInDate.add(const Duration(days: 3));
    final holdEndDateStr = Formatters.formatDate(holdEndDate);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        // Summary info
        _buildPropertySummaryRow('Khu trọ', widget.property.propertyName),
        const SizedBox(height: 12),
        _buildPropertySummaryRow(
            'Phòng', '${widget.room.roomCode} · ${widget.room.typeName}'),
        const SizedBox(height: 12),
        _buildPropertySummaryRow('Người đặt', '$userName · $userPhone'),
        const SizedBox(height: 16),

        // Deposit amount highlight banner
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          decoration: BoxDecoration(
            color: const Color(0xFFE6F4F2),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Text(
            'Tiền cọc cần thanh toán: ${Formatters.formatCurrency(widget.room.depositAmount)}',
            style: const TextStyle(
              color: Color(0xFF087775),
              fontWeight: FontWeight.w700,
              fontSize: 14,
            ),
          ),
        ),
        const SizedBox(height: 20),

        // Inputs: Ngày dọn vào dự kiến & Số người ở
        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFF8FAFC),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFE2E8F0)),
          ),
          child: Column(
            children: [
              // Ngày dọn vào
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Ngày dọn vào dự kiến',
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      fontSize: 13,
                      color: Color(0xFF334155),
                    ),
                  ),
                  const SizedBox(height: 6),
                  InkWell(
                    onTap: () async {
                      final now = DateTime.now();
                      final picked = await showDatePicker(
                        context: context,
                        initialDate: _moveInDate,
                        firstDate: now,
                        lastDate: now.add(const Duration(days: 14)),
                      );
                      if (picked != null) {
                        setState(() {
                          _moveInDate = picked;
                          _dateError = null;
                        });
                      }
                    },
                    child: Container(
                      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                      decoration: BoxDecoration(
                        color: Colors.white,
                        borderRadius: BorderRadius.circular(8),
                        border: Border.all(
                          color: _dateError != null
                              ? const Color(0xFFF43F5E)
                              : const Color(0xFFCBD5E1),
                        ),
                      ),
                      child: Row(
                        mainAxisAlignment: MainAxisAlignment.spaceBetween,
                        children: [
                          Text(
                            Formatters.formatDate(_moveInDate),
                            style: const TextStyle(
                              fontSize: 14,
                              color: Color(0xFF0F172A),
                            ),
                          ),
                          const Icon(Icons.calendar_month_outlined,
                              size: 18, color: Color(0xFF64748B)),
                        ],
                      ),
                    ),
                  ),
                  const SizedBox(height: 4),
                  const Text(
                    'Tối đa 14 ngày kể từ hôm nay',
                    style: TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                  ),
                  if (_dateError != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      _dateError!,
                      style: const TextStyle(fontSize: 12, color: Color(0xFFE11D48)),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: 14),

              // Số người ở
              Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Số người ở',
                    style: TextStyle(
                      fontWeight: FontWeight.w600,
                      fontSize: 13,
                      color: Color(0xFF334155),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 12),
                    decoration: BoxDecoration(
                      color: Colors.white,
                      borderRadius: BorderRadius.circular(8),
                      border: Border.all(
                        color: _occupantError != null
                            ? const Color(0xFFF43F5E)
                            : const Color(0xFFCBD5E1),
                      ),
                    ),
                    child: Row(
                      children: [
                        Expanded(
                          child: TextFormField(
                            controller: _occupantController,
                            keyboardType: TextInputType.number,
                            decoration: const InputDecoration(
                              border: InputBorder.none,
                              isDense: true,
                              contentPadding: EdgeInsets.symmetric(vertical: 10),
                            ),
                            onChanged: (val) {
                              final parsed = int.tryParse(val);
                              if (parsed != null) {
                                setState(() {
                                  _occupantCount = parsed;
                                  if (parsed > maxOccupancy) {
                                    _occupantError = 'Phòng ${widget.room.roomCode} chỉ được ở tối đa $maxOccupancy người.';
                                  } else if (parsed < 1) {
                                    _occupantError = 'Số người ở tối thiểu là 1.';
                                  } else {
                                    _occupantError = null;
                                  }
                                });
                              }
                            },
                          ),
                        ),
                        IconButton(
                          icon: const Icon(Icons.remove_circle_outline, size: 20),
                          onPressed: () {
                            if (_occupantCount > 1) {
                              setState(() {
                                _occupantCount--;
                                _occupantController.text = _occupantCount.toString();
                                if (_occupantCount > maxOccupancy) {
                                  _occupantError = 'Phòng ${widget.room.roomCode} chỉ được ở tối đa $maxOccupancy người.';
                                } else {
                                  _occupantError = null;
                                }
                              });
                            }
                          },
                          constraints: const BoxConstraints(),
                          padding: const EdgeInsets.all(4),
                          color: _occupantCount > 1 ? const Color(0xFF087775) : const Color(0xFFCBD5E1),
                        ),
                        IconButton(
                          icon: const Icon(Icons.add_circle_outline, size: 20),
                          onPressed: () {
                            if (_occupantCount < maxOccupancy) {
                              setState(() {
                                _occupantCount++;
                                _occupantController.text = _occupantCount.toString();
                                _occupantError = null;
                              });
                            } else {
                              setState(() {
                                _occupantError = 'Phòng ${widget.room.roomCode} chỉ được ở tối đa $maxOccupancy người.';
                              });
                            }
                          },
                          constraints: const BoxConstraints(),
                          padding: const EdgeInsets.all(4),
                          color: _occupantCount < maxOccupancy ? const Color(0xFF087775) : const Color(0xFFCBD5E1),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Tối đa $maxOccupancy người cho phòng ${widget.room.roomCode}',
                    style: const TextStyle(fontSize: 12, color: Color(0xFF64748B)),
                  ),
                  if (_occupantError != null) ...[
                    const SizedBox(height: 2),
                    Text(
                      _occupantError!,
                      style: const TextStyle(fontSize: 12, color: Color(0xFFE11D48)),
                    ),
                  ],
                ],
              ),
            ],
          ),
        ),
        const SizedBox(height: 16),

        // Text hướng dẫn chính sách thời hạn 15 phút & giữ phòng
        const Text(
          'Sau khi giữ phòng, bạn có 15 phút để thanh toán tiền cọc.',
          style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
        ),
        const SizedBox(height: 4),
        Text(
          'Điều khoản tiền cọc sẽ hiển thị trong hồ sơ trước khi giữ phòng. Phòng được giữ đến hết ngày $holdEndDateStr sau khi cọc.',
          style: const TextStyle(fontSize: 13, color: Color(0xFF64748B)),
        ),
        const SizedBox(height: 20),

        // ------------------ SECTION: HỒ SƠ NGƯỜI THUÊ CCCD ------------------
        _buildIdentitySection(),
        const SizedBox(height: 20),

        // Banners điều khoản & bảo mật
        Container(
          padding: const EdgeInsets.all(12),
          decoration: BoxDecoration(
            color: const Color(0xFFE6F4F2),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Text(
            _application?.depositAppliesToFirstMonth == false
                ? 'Cọc là khoản bảo đảm, không tự khấu trừ tiền phòng tháng đầu; việc hoàn cọc theo điều khoản booking.'
                : 'Cọc được khấu trừ vào tiền phòng theo điều khoản booking; phần tiền phòng còn lại và dịch vụ được tính trong hóa đơn.',
            style: const TextStyle(fontSize: 12, color: Color(0xFF087775), height: 1.4),
          ),
        ),
        const SizedBox(height: 8),
        const Text(
          'Ảnh được lưu riêng tư. Hồ sơ của Bạn phải đầy đủ; tải ảnh lên không có nghĩa hồ sơ đã được duyệt.',
          style: TextStyle(fontSize: 11, color: Color(0xFF64748B)),
        ),
        const SizedBox(height: 20),

        if (_generalError != null) ...[
          Text(
            _generalError!,
            style: const TextStyle(fontSize: 13, color: Color(0xFFE11D48)),
          ),
          const SizedBox(height: 12),
        ],

        // Nút chính: Giữ phòng và thanh toán cọc
        ElevatedButton(
          onPressed: (_isSubmittingApp || !_isReadyToSubmit) ? null : _submitApplicationAndProceed,
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF087775),
            disabledBackgroundColor: const Color(0xFF087775).withOpacity(0.4),
            foregroundColor: Colors.white,
            padding: const EdgeInsets.symmetric(vertical: 14),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            elevation: 0,
          ),
          child: _isSubmittingApp
              ? const SizedBox(
                  height: 20,
                  width: 20,
                  child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2),
                )
              : Text(
                  _isAppLoading
                      ? 'Đang xử lý hồ sơ…'
                      : _application?.bookingId != null
                          ? 'Tiếp tục thanh toán booking đã tạo'
                          : 'Giữ phòng và thanh toán cọc',
                  style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
                ),
        ),
      ],
    );
  }

  /// Section: Hồ sơ người thuê (Chuẩn bị hồ sơ CCCD & Người đứng tên dự kiến)
  Widget _buildIdentitySection() {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const Text(
          'Hồ sơ người thuê',
          style: TextStyle(
            fontWeight: FontWeight.w700,
            fontSize: 16,
            color: Color(0xFF0F172A),
          ),
        ),
        const SizedBox(height: 6),
        const Text(
          'Bạn cần cung cấp đủ CCCD hai mặt trước khi giữ phòng. Người ở cùng có thể bổ sung sau khi cọc, trước khi lập hợp đồng. Chuẩn bị hồ sơ chưa giữ phòng và chưa bắt đầu 15 phút thanh toán.',
          style: TextStyle(fontSize: 13, color: Color(0xFF64748B), height: 1.4),
        ),
        const SizedBox(height: 12),

        // Nút chuẩn bị hồ sơ hoặc Cập nhật & Tải lại hồ sơ (sử dụng Wrap để không bao giờ bị overflow)
        Wrap(
          spacing: 12,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            if (_application == null || _isMismatch)
              OutlinedButton(
                onPressed: _isAppLoading ? null : _prepareOrUpdateApplication,
                style: OutlinedButton.styleFrom(
                  foregroundColor: const Color(0xFF087775),
                  side: const BorderSide(color: Color(0xFF087775)),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                  padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
                ),
                child: _isAppLoading
                    ? const SizedBox(
                        width: 16,
                        height: 16,
                        child: CircularProgressIndicator(strokeWidth: 2, color: Color(0xFF087775)),
                      )
                    : Text(
                        _application != null
                            ? 'Cập nhật ngày và số người trong hồ sơ'
                            : 'Chuẩn bị hồ sơ CCCD',
                        style: const TextStyle(fontWeight: FontWeight.w600, fontSize: 13),
                      ),
              ),
            InkWell(
              onTap: _isAppLoading ? null : _reloadApplication,
              child: const Padding(
                padding: EdgeInsets.symmetric(vertical: 8),
                child: Text(
                  'Tải lại hồ sơ',
                  style: TextStyle(
                    fontSize: 13,
                    color: Color(0xFF087775),
                    decoration: TextDecoration.underline,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ),
          ],
        ),

        if (_appError != null) ...[
          const SizedBox(height: 8),
          Text(_appError!, style: const TextStyle(fontSize: 12, color: Color(0xFFE11D48))),
        ],

        // Khi hồ sơ đã được chuẩn bị, hiển thị Form nhập CCCD (Hình 2)
        if (_application != null && !_isMismatch) ...[
          const SizedBox(height: 16),
          _buildPersonCard(_application!.mainTenant),
        ],
      ],
    );
  }

  /// Card form nhập thông tin CCCD của người đứng tên dự kiến (Hình 2)
  Widget _buildPersonCard(ApplicationPersonModel? person) {
    if (person == null) return const SizedBox.shrink();

    String statusText = 'Thiếu thông tin';
    Color statusColor = const Color(0xFFD97706);
    if (person.hasFront && person.hasBack && (person.maskedIdentityNumber != null || _idNumberController.text.length == 12)) {
      statusText = 'Đã đủ thông tin';
      statusColor = const Color(0xFF087775);
    }

    return Container(
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(16),
        border: Border.all(color: const Color(0xFFE2E8F0)),
      ),
      padding: const EdgeInsets.all(16),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Tiêu đề card
          const Text(
            'Bạn – Người đứng tên dự kiến',
            style: TextStyle(
              fontSize: 15,
              fontWeight: FontWeight.w700,
              color: Color(0xFF0F172A),
            ),
          ),
          const SizedBox(height: 6),

          // Trạng thái
          Row(
            children: [
              const Text('Trạng thái: ', style: TextStyle(fontSize: 13, color: Color(0xFF64748B))),
              Text(
                statusText,
                style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700, color: statusColor),
              ),
            ],
          ),
          const SizedBox(height: 14),

          // Họ tên của bạn
          const Text(
            'Họ tên của bạn',
            style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF334155)),
          ),
          const SizedBox(height: 6),
          TextFormField(
            controller: _nameController,
            decoration: InputDecoration(
              contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(8),
                borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
              ),
              isDense: true,
            ),
            onChanged: (val) => setState(() => _personDirty = true),
          ),
          const SizedBox(height: 12),

          // Số CCCD (12 chữ số)
          const Text(
            'Số CCCD (12 chữ số)',
            style: TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF334155)),
          ),
          const SizedBox(height: 6),
          TextFormField(
            controller: _idNumberController,
            keyboardType: TextInputType.number,
            maxLength: 12,
            decoration: InputDecoration(
              counterText: '',
              hintText: person.maskedIdentityNumber ?? 'Nhập 12 chữ số',
              contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(8),
                borderSide: const BorderSide(color: Color(0xFFCBD5E1)),
              ),
              isDense: true,
            ),
            onChanged: (val) => setState(() => _personDirty = true),
          ),
          const SizedBox(height: 8),

          // Checkbox đồng ý cung cấp thông tin
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              SizedBox(
                width: 24,
                height: 24,
                child: Checkbox(
                  value: _consentConfirmed,
                  activeColor: const Color(0xFF087775),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(4)),
                  onChanged: (val) {
                    setState(() {
                      _consentConfirmed = val ?? false;
                      _personDirty = true;
                    });
                  },
                ),
              ),
              const SizedBox(width: 8),
              const Expanded(
                child: Text(
                  'Tôi đồng ý cung cấp thông tin và ảnh CCCD để thực hiện thuê phòng.',
                  style: TextStyle(fontSize: 13, color: Color(0xFF334155), height: 1.3),
                ),
              ),
            ],
          ),
          const SizedBox(height: 14),

          // Nút lưu thông tin
          OutlinedButton(
            onPressed: _isSavingPerson ? null : () => _savePersonInfo(),
            style: OutlinedButton.styleFrom(
              foregroundColor: const Color(0xFF087775),
              side: const BorderSide(color: Color(0xFF087775)),
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
            ),
            child: _isSavingPerson
                ? const SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2, color: Color(0xFF087775)),
                  )
                : const Text('Lưu thông tin của bạn', style: TextStyle(fontWeight: FontWeight.w600, fontSize: 13)),
          ),

          if (_personDirty) ...[
            const SizedBox(height: 6),
            const Text(
              'Thông tin chưa lưu. Khi chọn ảnh, hệ thống sẽ lưu thông tin trước rồi tải ảnh lên.',
              style: TextStyle(fontSize: 11, color: Color(0xFFB45309)),
            ),
          ],
          if (_personError != null) ...[
            const SizedBox(height: 6),
            Text(_personError!, style: const TextStyle(fontSize: 12, color: Color(0xFFE11D48))),
          ],
          const SizedBox(height: 8),

          // Text gợi ý chọn ảnh
          Text(
            _consentConfirmed
                ? 'Chọn ảnh mặt trước và mặt sau (JPEG, PNG hoặc WebP, tối đa 5 MiB mỗi ảnh). Nếu chưa lưu, thông tin của người này sẽ được lưu tự động trước khi tải ảnh.'
                : 'Để bật nút chọn ảnh, hãy tích ô đồng ý cung cấp thông tin và ảnh CCCD ở trên.',
            style: const TextStyle(fontSize: 12, color: Color(0xFF64748B), height: 1.3),
          ),
          const SizedBox(height: 16),

          // Hàng 2 khung upload: Mặt trước & Mặt sau
          Row(
            children: [
              Expanded(
                child: _buildUploadBox(
                  title: 'Mặt trước',
                  hasImage: person.hasFront,
                  isUploading: _isUploadingFront,
                  side: 'Front',
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _buildUploadBox(
                  title: 'Mặt sau',
                  hasImage: person.hasBack,
                  isUploading: _isUploadingBack,
                  side: 'Back',
                ),
              ),
            ],
          ),
        ],
      ),
    );
  }

  /// Khung upload mặt trước / mặt sau CCCD (Hình 2)
  Widget _buildUploadBox({
    required String title,
    required bool hasImage,
    required bool isUploading,
    required String side,
  }) {
    final enabled = _consentConfirmed && !_isAppLoading;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          title,
          style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Color(0xFF0F172A)),
        ),
        const SizedBox(height: 8),
        InkWell(
          onTap: (isUploading || !enabled) ? null : () => _pickAndUploadImage(side),
          borderRadius: BorderRadius.circular(16),
          child: Container(
            height: 130,
            decoration: BoxDecoration(
              color: hasImage ? const Color(0xFFF0FDF9) : Colors.white,
              borderRadius: BorderRadius.circular(16),
              border: Border.all(
                color: hasImage ? const Color(0xFF087775) : const Color(0xFF6B8985),
                width: 2,
              ),
            ),
            alignment: Alignment.center,
            child: isUploading
                ? const CircularProgressIndicator(color: Color(0xFF087775), strokeWidth: 2)
                : hasImage
                    ? Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: const [
                          Icon(Icons.check_circle_rounded, size: 40, color: Color(0xFF087775)),
                          SizedBox(height: 6),
                          Text(
                            'Đã tải ảnh',
                            style: TextStyle(
                              fontSize: 13,
                              fontWeight: FontWeight.w700,
                              color: Color(0xFF087775),
                            ),
                          ),
                          SizedBox(height: 2),
                          Text(
                            'Bấm để chọn lại',
                            style: TextStyle(fontSize: 10, color: Color(0xFF64748B)),
                          ),
                        ],
                      )
                    : Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Icon(
                            Icons.add_rounded,
                            size: 48,
                            color: enabled ? const Color(0xFFB4D394) : const Color(0xFFCBD5E1),
                          ),
                          const SizedBox(height: 4),
                          Text(
                            'Bấm để chọn ảnh',
                            style: TextStyle(
                              fontSize: 11,
                              color: enabled ? const Color(0xFF64748B) : const Color(0xFF94A3B8),
                            ),
                          ),
                        ],
                      ),
          ),
        ),
        if (hasImage) ...[
          const SizedBox(height: 6),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              InkWell(
                onTap: () => _previewImage(side),
                child: const Text(
                  'Xem ảnh',
                  style: TextStyle(
                    fontSize: 11,
                    color: Color(0xFF087775),
                    decoration: TextDecoration.underline,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
              const SizedBox(width: 14),
              InkWell(
                onTap: () => _deleteImage(side),
                child: const Text(
                  'Xóa ảnh',
                  style: TextStyle(
                    fontSize: 11,
                    color: Color(0xFFE11D48),
                    decoration: TextDecoration.underline,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ],
      ],
    );
  }

  /// Bước 2: Nhấn để thanh toán qua payOS (Hình 5)
  Widget _buildStep2PayOS() {
    final booking = _createdBooking;
    if (booking == null) return const SizedBox.shrink();

    final expiryFormatted = booking.expiryDate != null
        ? Formatters.formatDateTime(booking.expiryDate)
        : '15 phút kể từ lúc đặt';

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text('Khu trọ', style: TextStyle(fontSize: 12, color: Color(0xFF64748B))),
                  const SizedBox(height: 2),
                  Text(
                    widget.property.propertyName,
                    style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Color(0xFF0F172A)),
                  ),
                ],
              ),
            ),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                const Text('Phòng', style: TextStyle(fontSize: 12, color: Color(0xFF64748B))),
                const SizedBox(height: 2),
                Text(
                  widget.room.roomCode,
                  style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700, color: Color(0xFF0F172A)),
                ),
              ],
            ),
          ],
        ),
        const SizedBox(height: 12),
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                const Text('Dự kiến nhận phòng', style: TextStyle(fontSize: 12, color: Color(0xFF64748B))),
                const SizedBox(height: 2),
                Text(
                  'Dọn vào ${Formatters.formatDate(booking.moveInDate)} · ${booking.occupantCount} người',
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF0F172A)),
                ),
              ],
            ),
            Column(
              crossAxisAlignment: CrossAxisAlignment.end,
              children: [
                const Text('Hạn thanh toán cọc', style: TextStyle(fontSize: 12, color: Color(0xFF64748B))),
                const SizedBox(height: 2),
                Text(
                  expiryFormatted,
                  style: const TextStyle(fontSize: 13, fontWeight: FontWeight.w600, color: Color(0xFF0F172A)),
                ),
              ],
            ),
          ],
        ),
        const SizedBox(height: 16),

        Container(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
          decoration: BoxDecoration(
            color: const Color(0xFFE6F4F2),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Text(
            'Tiền cọc cần thanh toán: ${Formatters.formatCurrency(booking.depositAmount)}',
            style: const TextStyle(
              color: Color(0xFF087775),
              fontWeight: FontWeight.w700,
              fontSize: 14,
            ),
          ),
        ),
        const SizedBox(height: 16),

        const Text(
          'Tiền cọc được trừ vào tiền phòng tháng đầu tiên khi lập hợp đồng.',
          style: TextStyle(fontSize: 13, color: Color(0xFF64748B)),
        ),
        const SizedBox(height: 16),

        Container(
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: const Color(0xFFF0FDF9),
            borderRadius: BorderRadius.circular(12),
            border: Border.all(color: const Color(0xFFCCFBF1)),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: const [
              Text(
                'Thanh toán cọc qua payOS',
                style: TextStyle(
                  fontWeight: FontWeight.w700,
                  fontSize: 14,
                  color: Color(0xFF0F172A),
                ),
              ),
              SizedBox(height: 6),
              Text(
                'Mở trang thanh toán để quét mã hoặc chuyển khoản. Booking chỉ được xác nhận sau khi hệ thống nhận kết quả thanh toán.',
                style: TextStyle(
                  fontSize: 13,
                  color: Color(0xFF475569),
                  height: 1.4,
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 20),

        ElevatedButton(
          onPressed: _openPayOS,
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFF087775),
            foregroundColor: Colors.white,
            padding: const EdgeInsets.symmetric(vertical: 14),
            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
            elevation: 0,
          ),
          child: Text(
            'Thanh toán ${Formatters.formatCurrency(booking.depositAmount)} qua payOS',
            style: const TextStyle(fontWeight: FontWeight.w700, fontSize: 15),
          ),
        ),
        const SizedBox(height: 12),

        TextButton(
          onPressed: () => context.go(AppRoutes.customerBookings),
          child: const Text(
            'Về danh sách đặt cọc của tôi',
            style: TextStyle(color: Color(0xFF087775), fontWeight: FontWeight.w600),
          ),
        ),
      ],
    );
  }

  Widget _buildPropertySummaryRow(String label, String value) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          width: 90,
          child: Text(
            label,
            style: const TextStyle(fontSize: 13, color: Color(0xFF64748B)),
          ),
        ),
        Expanded(
          child: Text(
            value,
            style: const TextStyle(
              fontSize: 14,
              fontWeight: FontWeight.w700,
              color: Color(0xFF0F172A),
            ),
          ),
        ),
      ],
    );
  }
}
