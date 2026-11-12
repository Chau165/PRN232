import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/booking_service.dart';
import '../../../core/services/contract_service.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/app_text_field.dart';
import '../../customer/models/customer_models.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';

class CreateContractScreen extends StatefulWidget {
  const CreateContractScreen({super.key});

  @override
  State<CreateContractScreen> createState() => _CreateContractScreenState();
}

class _CreateContractScreenState extends State<CreateContractScreen> {
  final ContractService _contractService = ContractService();
  final BookingService _bookingService = BookingService();

  ManagerRoomModel? _selectedRoom;
  CustomerBookingModel? _selectedBooking;
  List<CustomerBookingModel> _depositedBookings = [];

  final TextEditingController _tenantNameController = TextEditingController();
  final TextEditingController _tenantPhoneController = TextEditingController();
  final TextEditingController _tenantCccdController = TextEditingController();
  final TextEditingController _rentController = TextEditingController();
  final TextEditingController _depositController = TextEditingController();

  DateTime _startDate = DateTime.now();
  DateTime _endDate = DateTime.now().add(const Duration(days: 365)); // 1 năm
  bool _isCreating = false;

  @override
  void initState() {
    super.initState();
    _loadDepositedBookings();

    try {
      _selectedRoom = ManagerMockData.rooms.firstWhere(
        (r) => r.isAvailable || r.isReserved,
      );
      _updatePriceFromRoom();
    } catch (_) {
      if (ManagerMockData.rooms.isNotEmpty) {
        _selectedRoom = ManagerMockData.rooms.first;
        _updatePriceFromRoom();
      }
    }
  }

  Future<void> _loadDepositedBookings() async {
    try {
      final list = await _bookingService.getBookings(status: 'Deposited');
      if (mounted) {
        setState(() {
          _depositedBookings = list;
          if (_depositedBookings.isNotEmpty) {
            _selectedBooking = _depositedBookings.first;
            _applyBookingInfo(_selectedBooking!);
          }
        });
      }
    } catch (_) {}
  }

  void _applyBookingInfo(CustomerBookingModel booking) {
    _tenantNameController.text = 'Khách đặt cọc ${booking.roomCode}';
    _tenantPhoneController.text = '0901234567';
    _tenantCccdController.text = '079201009876';
    _rentController.text = booking.depositAmount.toString();
    _depositController.text = booking.depositAmount.toString();
  }

  void _updatePriceFromRoom() {
    if (_selectedRoom != null) {
      _rentController.text = _selectedRoom!.price.toString();
      _depositController.text = _selectedRoom!.depositAmount.toString();
    }
  }

  @override
  void dispose() {
    _tenantNameController.dispose();
    _tenantPhoneController.dispose();
    _tenantCccdController.dispose();
    _rentController.dispose();
    _depositController.dispose();
    super.dispose();
  }

  void _submitContract() async {
    if (_selectedRoom == null) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng chọn phòng thuê.')),
      );
      return;
    }

    if (_tenantNameController.text.trim().isEmpty ||
        _tenantPhoneController.text.trim().isEmpty ||
        _tenantCccdController.text.trim().isEmpty) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Vui lòng điền đầy đủ thông tin khách thuê.')),
      );
      return;
    }

    setState(() => _isCreating = true);

    // Nếu tạo từ Booking đã đặt cọc (theo đúng flow BE)
    if (_selectedBooking != null) {
      try {
        await _contractService.createContractFromBooking(
          bookingId: _selectedBooking!.id,
          startDate: _startDate,
          endDate: _endDate,
        );
        if (!mounted) return;
        setState(() => _isCreating = false);
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(
            content: Text('Đã lập hợp đồng chính thức từ đơn cọc thành công!'),
            backgroundColor: AppColors.primary,
            behavior: SnackBarBehavior.floating,
          ),
        );
        Navigator.pop(context, true);
        return;
      } catch (e) {
        // Fallback local contract
      }
    }

    final rent = num.tryParse(_rentController.text) ?? _selectedRoom!.price;
    final deposit = num.tryParse(_depositController.text) ?? _selectedRoom!.depositAmount;

    final newContract = ManagerContractModel(
      id: DateTime.now().millisecondsSinceEpoch,
      contractCode: 'HD-2026-${_selectedRoom!.roomCode.replaceAll(RegExp(r'[^0-9]'), '')}',
      propertyId: 1,
      propertyName: ManagerMockData.currentPropertyName,
      roomId: _selectedRoom!.roomId,
      roomCode: _selectedRoom!.roomCode,
      roomTypeName: _selectedRoom!.typeName,
      primaryTenantName: _tenantNameController.text.trim(),
      tenantPhone: _tenantPhoneController.text.trim(),
      tenantCccd: _tenantCccdController.text.trim(),
      startDate: _startDate,
      endDate: _endDate,
      monthlyRent: rent,
      depositAmount: deposit,
      status: 'Active',
      notes: 'Hợp đồng mới lập trên mobile app.',
    );

    ManagerMockData.contracts.insert(0, newContract);

    // Cập nhật trạng thái phòng
    _selectedRoom!.status = 'Occupied';
    _selectedRoom!.tenants.add(
      TenantModel(
        id: DateTime.now().millisecondsSinceEpoch,
        name: _tenantNameController.text.trim(),
        phone: _tenantPhoneController.text.trim(),
        cccd: _tenantCccdController.text.trim(),
        startDate: _startDate,
      ),
    );

    if (!mounted) return;
    setState(() => _isCreating = false);

    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text('Đã lập hợp đồng ${newContract.contractCode} thành công!'),
        backgroundColor: AppColors.primary,
        behavior: SnackBarBehavior.floating,
      ),
    );

    Navigator.pop(context, true);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: AppColors.background,
      appBar: AppBar(
        title: const Text('Lập Hợp Đồng Thuê Mới'),
        backgroundColor: Colors.white,
        elevation: 0,
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // Option: Chọn từ đơn cọc chờ lập hợp đồng
            if (_depositedBookings.isNotEmpty) ...[
              Text('Đơn đặt cọc phòng chờ lập hợp đồng', style: AppTypography.headingSmall),
              const SizedBox(height: 8),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 14),
                decoration: BoxDecoration(
                  color: AppColors.primaryLight,
                  borderRadius: BorderRadius.circular(12),
                  border: Border.all(color: AppColors.primary),
                ),
                child: DropdownButtonHideUnderline(
                  child: DropdownButton<CustomerBookingModel>(
                    value: _selectedBooking,
                    isExpanded: true,
                    icon: const Icon(Icons.arrow_drop_down_rounded, color: AppColors.primary),
                    items: _depositedBookings.map((b) {
                      return DropdownMenuItem<CustomerBookingModel>(
                        value: b,
                        child: Text(
                          '${b.bookingCode} • ${b.roomCode} (Cọc: ${Formatters.formatCurrency(b.depositAmount)})',
                          style: AppTypography.labelMedium.copyWith(color: AppColors.primary),
                        ),
                      );
                    }).toList(),
                    onChanged: (val) {
                      if (val != null) {
                        setState(() {
                          _selectedBooking = val;
                          _applyBookingInfo(val);
                        });
                      }
                    },
                  ),
                ),
              ),
              const SizedBox(height: 20),
            ],

            // 1. Chọn phòng
            Text('1. Chọn phòng cho thuê', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 14),
              decoration: BoxDecoration(
                color: Colors.white,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColors.border),
              ),
              child: DropdownButtonHideUnderline(
                child: DropdownButton<ManagerRoomModel>(
                  value: _selectedRoom,
                  isExpanded: true,
                  icon: const Icon(Icons.arrow_drop_down_rounded, color: AppColors.primary),
                  items: ManagerMockData.rooms.map((room) {
                    final statusText = room.isAvailable ? 'Trống' : (room.isReserved ? 'Đã cọc' : 'Đang thuê');
                    return DropdownMenuItem<ManagerRoomModel>(
                      value: room,
                      child: Text(
                        '${room.roomCode} - ${room.typeName} ($statusText)',
                        style: AppTypography.bodyMedium,
                      ),
                    );
                  }).toList(),
                  onChanged: (val) {
                    if (val != null) {
                      setState(() {
                        _selectedRoom = val;
                        _updatePriceFromRoom();
                      });
                    }
                  },
                ),
              ),
            ),
            const SizedBox(height: 20),

            // 2. Thông tin khách thuê
            Text('2. Thông tin khách thuê chính', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              child: Column(
                children: [
                  AppTextField(
                    label: 'Họ và tên khách thuê *',
                    hint: 'Ví dụ: NGUYỄN VĂN AN',
                    controller: _tenantNameController,
                  ),
                  const SizedBox(height: 12),
                  AppTextField(
                    label: 'Số điện thoại *',
                    hint: 'Ví dụ: 0901234567',
                    controller: _tenantPhoneController,
                    keyboardType: TextInputType.phone,
                  ),
                  const SizedBox(height: 12),
                  AppTextField(
                    label: 'Số CCCD / CMND *',
                    hint: 'Ví dụ: 079201009876',
                    controller: _tenantCccdController,
                    keyboardType: TextInputType.number,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // 3. Thời hạn & Giá thuê
            Text('3. Thời hạn & Điều khoản tài chính', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              child: Column(
                children: [
                  Row(
                    children: [
                      Expanded(
                        child: InkWell(
                          onTap: () async {
                            final picked = await showDatePicker(
                              context: context,
                              initialDate: _startDate,
                              firstDate: DateTime.now().subtract(const Duration(days: 30)),
                              lastDate: DateTime.now().add(const Duration(days: 90)),
                            );
                            if (picked != null) setState(() => _startDate = picked);
                          },
                          child: Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: AppColors.surfaceVariant,
                              borderRadius: BorderRadius.circular(10),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text('Ngày bắt đầu', style: AppTypography.bodySmall),
                                const SizedBox(height: 4),
                                Text(Formatters.formatDate(_startDate), style: AppTypography.labelLarge),
                              ],
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(width: 12),
                      Expanded(
                        child: InkWell(
                          onTap: () async {
                            final picked = await showDatePicker(
                              context: context,
                              initialDate: _endDate,
                              firstDate: _startDate,
                              lastDate: _startDate.add(const Duration(days: 365 * 3)),
                            );
                            if (picked != null) setState(() => _endDate = picked);
                          },
                          child: Container(
                            padding: const EdgeInsets.all(12),
                            decoration: BoxDecoration(
                              color: AppColors.surfaceVariant,
                              borderRadius: BorderRadius.circular(10),
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text('Ngày kết thúc', style: AppTypography.bodySmall),
                                const SizedBox(height: 4),
                                Text(Formatters.formatDate(_endDate), style: AppTypography.labelLarge),
                              ],
                            ),
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  AppTextField(
                    label: 'Tiền thuê phòng hàng tháng (VNĐ) *',
                    hint: 'Nhập giá thuê',
                    controller: _rentController,
                    keyboardType: TextInputType.number,
                  ),
                  const SizedBox(height: 14),
                  AppTextField(
                    label: 'Tiền cọc đảm bảo hợp đồng (VNĐ) *',
                    hint: 'Nhập tiền cọc',
                    controller: _depositController,
                    keyboardType: TextInputType.number,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 28),

            // Submit Button
            AppButton(
              text: 'Ký & Kích hoạt hợp đồng',
              isLoading: _isCreating,
              onPressed: _submitContract,
            ),
            const SizedBox(height: 16),
          ],
        ),
      ),
    );
  }
}
