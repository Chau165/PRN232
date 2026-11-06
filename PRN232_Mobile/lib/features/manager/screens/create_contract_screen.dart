import 'package:flutter/material.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_button.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/app_text_field.dart';
import '../data/manager_mock_data.dart';
import '../models/manager_models.dart';

class CreateContractScreen extends StatefulWidget {
  const CreateContractScreen({super.key});

  @override
  State<CreateContractScreen> createState() => _CreateContractScreenState();
}

class _CreateContractScreenState extends State<CreateContractScreen> {
  ManagerRoomModel? _selectedRoom;
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
    // Default to first available or reserved room
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
    await Future.delayed(const Duration(milliseconds: 700));
    if (!mounted) return;

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
            // Select Room
            Text('1. Chọn phòng cho thuê', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
              child: DropdownButtonHideUnderline(
                child: DropdownButton<ManagerRoomModel>(
                  value: _selectedRoom,
                  isExpanded: true,
                  hint: const Text('Chọn phòng'),
                  items: ManagerMockData.rooms.map((room) {
                    return DropdownMenuItem<ManagerRoomModel>(
                      value: room,
                      child: Text(
                        '${room.roomCode} • ${room.typeName} (Tầng ${room.floor}) [${room.status}]',
                        style: AppTypography.labelMedium,
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

            // Tenant information
            Text('2. Thông tin khách thuê (Bên B)', style: AppTypography.headingSmall),
            const SizedBox(height: 8),
            AppCard(
              child: Column(
                children: [
                  AppTextField(
                    label: 'Họ và tên khách thuê',
                    hint: 'Ví dụ: NGUYEN VAN A',
                    controller: _tenantNameController,
                  ),
                  const SizedBox(height: 12),
                  AppTextField(
                    label: 'Số điện thoại liên hệ',
                    hint: '0901 234 567',
                    controller: _tenantPhoneController,
                    keyboardType: TextInputType.phone,
                  ),
                  const SizedBox(height: 12),
                  AppTextField(
                    label: 'Số CCCD (12 chữ số)',
                    hint: '07920100xxxx',
                    controller: _tenantCccdController,
                    keyboardType: TextInputType.number,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 20),

            // Rental term & pricing
            Text('3. Thời hạn & Giá thuê', style: AppTypography.headingSmall),
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
                              lastDate: DateTime.now().add(const Duration(days: 60)),
                            );
                            if (picked != null) {
                              setState(() => _startDate = picked);
                            }
                          },
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Ngày bắt đầu', style: AppTypography.bodySmall),
                              const SizedBox(height: 4),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                                decoration: BoxDecoration(
                                  color: AppColors.surfaceVariant,
                                  borderRadius: BorderRadius.circular(8),
                                  border: Border.all(color: AppColors.border),
                                ),
                                child: Text(Formatters.formatDate(_startDate), style: AppTypography.labelLarge),
                              ),
                            ],
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
                              lastDate: _startDate.add(const Duration(days: 1000)),
                            );
                            if (picked != null) {
                              setState(() => _endDate = picked);
                            }
                          },
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              Text('Ngày kết thúc', style: AppTypography.bodySmall),
                              const SizedBox(height: 4),
                              Container(
                                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
                                decoration: BoxDecoration(
                                  color: AppColors.surfaceVariant,
                                  borderRadius: BorderRadius.circular(8),
                                  border: Border.all(color: AppColors.border),
                                ),
                                child: Text(Formatters.formatDate(_endDate), style: AppTypography.labelLarge),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 14),
                  AppTextField(
                    label: 'Tiền thuê hàng tháng (VNĐ)',
                    hint: '3200000',
                    controller: _rentController,
                    keyboardType: TextInputType.number,
                  ),
                  const SizedBox(height: 14),
                  AppTextField(
                    label: 'Tiền đặt cọc (VNĐ)',
                    hint: '3200000',
                    controller: _depositController,
                    keyboardType: TextInputType.number,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),

            AppButton(
              text: 'Tạo & Ký kết hợp đồng',
              isLoading: _isCreating,
              onPressed: _submitContract,
            ),
            const SizedBox(height: 20),
          ],
        ),
      ),
    );
  }
}
