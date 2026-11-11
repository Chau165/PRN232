import 'package:dio/dio.dart';
import 'package:intl/intl.dart';
import '../../features/customer/data/customer_mock_data.dart';
import '../../features/customer/models/customer_models.dart';
import '../../features/manager/data/manager_mock_data.dart';
import '../../features/manager/models/manager_models.dart';
import '../network/api_endpoints.dart';
import '../network/dio_client.dart';

class ContractService {
  final Dio _dio = DioClient.instance;
  final DateFormat _dateFormat = DateFormat('yyyy-MM-dd');

  /// Lấy danh sách hợp đồng (Customer lấy của mình, Manager lấy của khu trọ phụ trách)
  Future<List<CustomerContractModel>> getCustomerContracts() async {
    try {
      final response = await _dio.get(ApiEndpoints.contracts);
      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          final contracts = <CustomerContractModel>[];
          for (final item in list) {
            final c = CustomerContractModel.fromJson(item as Map<String, dynamic>);
            // Lấy thêm timeline công nợ cho hợp đồng này
            try {
              final timeline = await getBillingTimeline(c.id);
              contracts.add(CustomerContractModel(
                id: c.id,
                contractCode: c.contractCode,
                propertyId: c.propertyId,
                propertyName: c.propertyName,
                propertyAddress: c.propertyAddress,
                roomId: c.roomId,
                roomCode: c.roomCode,
                roomTypeName: c.roomTypeName,
                primaryTenantName: c.primaryTenantName,
                tenantCccd: c.tenantCccd,
                tenantPhone: c.tenantPhone,
                startDate: c.startDate,
                endDate: c.endDate,
                monthlyRent: c.monthlyRent,
                depositAmount: c.depositAmount,
                paymentCycleMonths: c.paymentCycleMonths,
                terms: c.terms,
                status: c.status,
                billingTimeline: timeline.isNotEmpty ? timeline : c.billingTimeline,
              ));
            } catch (_) {
              contracts.add(c);
            }
          }
          return contracts;
        }
      }
      return CustomerMockData.contracts;
    } catch (_) {
      return CustomerMockData.contracts;
    }
  }

  /// Lấy danh sách hợp đồng cho Manager
  Future<List<ManagerContractModel>> getManagerContracts() async {
    try {
      final response = await _dio.get(ApiEndpoints.contracts);
      if (response.data is List) {
        final list = response.data as List;
        if (list.isNotEmpty) {
          return list.map((c) => ManagerContractModel.fromJson(c as Map<String, dynamic>)).toList();
        }
      }
      return ManagerMockData.contracts;
    } catch (_) {
      return ManagerMockData.contracts;
    }
  }

  /// Lấy timeline công nợ từng kỳ thanh toán của hợp đồng
  Future<List<ContractTimelineItem>> getBillingTimeline(int contractId) async {
    try {
      final response = await _dio.get(ApiEndpoints.contractBillingTimeline(contractId));
      if (response.data is Map && response.data.containsKey('periods') && response.data['periods'] is List) {
        final periods = response.data['periods'] as List;
        return periods.map((p) => ContractTimelineItem.fromJson(p as Map<String, dynamic>)).toList();
      }
      return [];
    } catch (_) {
      return [];
    }
  }

  /// Lập hợp đồng mới từ một đơn cọc đã thanh toán (Booking có status = Deposited)
  Future<ManagerContractModel> createContractFromBooking({
    required int bookingId,
    required DateTime startDate,
    required DateTime endDate,
  }) async {
    try {
      final response = await _dio.post(
        ApiEndpoints.contracts,
        data: {
          'bookingId': bookingId,
          'startDate': _dateFormat.format(startDate),
          'endDate': _dateFormat.format(endDate),
        },
      );

      final data = response.data as Map<String, dynamic>;
      final contract = ManagerContractModel.fromJson(data);
      ManagerMockData.contracts.insert(0, contract);
      return contract;
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Lập hợp đồng không thành công.');
    } catch (e) {
      throw Exception('Lỗi kết nối máy chủ: $e');
    }
  }

  /// Gia hạn hợp đồng (Manager)
  Future<void> extendContract(int contractId, DateTime newEndDate) async {
    try {
      await _dio.put(
        ApiEndpoints.extendContract(contractId),
        data: {
          'newEndDate': _dateFormat.format(newEndDate),
        },
      );
      final idx = ManagerMockData.contracts.indexWhere((c) => c.id == contractId);
      if (idx != -1) {
        ManagerMockData.contracts[idx].endDate = newEndDate;
      }
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Gia hạn hợp đồng thất bại.');
    } catch (_) {
      final idx = ManagerMockData.contracts.indexWhere((c) => c.id == contractId);
      if (idx != -1) {
        ManagerMockData.contracts[idx].endDate = newEndDate;
      }
    }
  }

  /// Thanh lý hợp đồng & quyết toán hoàn cọc (Manager)
  Future<void> terminateContract(int contractId) async {
    try {
      await _dio.post(
        ApiEndpoints.terminateContract(contractId),
        data: {
          'checkoutConfirmed': true,
          'roomReadyForRental': true,
          'outstandingDebtAcknowledged': true,
        },
      );
      final idx = ManagerMockData.contracts.indexWhere((c) => c.id == contractId);
      if (idx != -1) {
        ManagerMockData.contracts[idx].status = 'Terminated';
      }
    } on DioException catch (e) {
      throw Exception(e.error?.toString() ?? 'Thanh lý hợp đồng thất bại.');
    } catch (_) {
      final idx = ManagerMockData.contracts.indexWhere((c) => c.id == contractId);
      if (idx != -1) {
        ManagerMockData.contracts[idx].status = 'Terminated';
      }
    }
  }

  /// Đánh dấu phòng đã vệ sinh / kiểm tra sẵn sàng đón khách
  Future<void> markRoomReady(int contractId) async {
    try {
      await _dio.post(ApiEndpoints.contractRoomReady(contractId));
    } catch (_) {}
  }
}
