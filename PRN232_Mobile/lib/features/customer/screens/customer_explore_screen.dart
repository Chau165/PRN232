import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/services/property_service.dart';
import '../data/customer_mock_data.dart';
import '../models/customer_models.dart';
import '../widgets/explore_map_view.dart';
import '../widgets/property_card_widget.dart';

class CustomerExploreScreen extends StatefulWidget {
  const CustomerExploreScreen({super.key});

  @override
  State<CustomerExploreScreen> createState() => _CustomerExploreScreenState();
}

class _CustomerExploreScreenState extends State<CustomerExploreScreen> {
  final PropertyService _propertyService = PropertyService();
  final TextEditingController _searchController = TextEditingController();
  final ScrollController _scrollController = ScrollController();

  bool _isMapView = false;
  String _selectedDistrict = 'Tất cả';
  List<PropertyItem> _properties = CustomerMockData.properties;
  bool _isLoading = false;

  int _currentPage = 1;
  int _totalCount = 0;
  final int _pageSize = 10;

  final List<String> _districts = [
    'Tất cả',
    'Gò Vấp',
    'Bình Thạnh',
    'TP. Thủ Đức',
    'Quận 7',
    'Dưới 3 triệu',
  ];

  @override
  void initState() {
    super.initState();
    _loadProperties(page: 1);
  }

  Future<void> _loadProperties({int page = 1}) async {
    setState(() {
      _isLoading = true;
      _currentPage = page;
    });

    final keyword = _searchController.text.trim();
    final isPriceFilter = _selectedDistrict == 'Dưới 3 triệu';
    final districtFilter = (_selectedDistrict != 'Tất cả' && !isPriceFilter)
        ? _selectedDistrict
        : null;
    final maxPriceFilter = isPriceFilter ? 3000000 : null;

    try {
      final paged = await _propertyService.getPublicPropertiesPaged(
        keyword: keyword.isNotEmpty ? keyword : null,
        district: districtFilter,
        maxPrice: maxPriceFilter,
        page: _currentPage,
        pageSize: _pageSize,
      );

      if (mounted) {
        setState(() {
          _properties = paged.items;
          _totalCount = paged.totalCount;
          _currentPage = paged.page;
          _isLoading = false;
        });

        // Cuộn lên đầu trang danh sách nếu đang ở vị trí cuộn
        if (_scrollController.hasClients) {
          _scrollController.animateTo(
            0,
            duration: const Duration(milliseconds: 300),
            curve: Curves.easeOut,
          );
        }
      }
    } catch (_) {
      if (mounted) {
        setState(() => _isLoading = false);
      }
    }
  }

  @override
  void dispose() {
    _searchController.dispose();
    _scrollController.dispose();
    super.dispose();
  }

  void _navigateToDetail(PropertyItem property) {
    context.push('/property-detail', extra: property);
  }

  int get _totalPages {
    if (_totalCount == 0) return 1;
    return (_totalCount / _pageSize).ceil();
  }

  @override
  Widget build(BuildContext context) {
    final totalPages = _totalPages;

    return Scaffold(
      backgroundColor: AppColors.background,
      body: Column(
        children: [
          // Search & Filter Bar
          Container(
            padding: const EdgeInsets.fromLTRB(16, 12, 16, 10),
            color: Colors.white,
            child: Column(
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Container(
                        height: 46,
                        decoration: BoxDecoration(
                          color: AppColors.surfaceVariant,
                          borderRadius: BorderRadius.circular(12),
                          border: Border.all(color: AppColors.borderLight),
                        ),
                        child: TextField(
                          controller: _searchController,
                          textInputAction: TextInputAction.search,
                          onSubmitted: (_) => _loadProperties(page: 1),
                          style: AppTypography.bodyMedium,
                          decoration: InputDecoration(
                            hintText: 'Tìm theo quận, tên đường, khu trọ...',
                            hintStyle: AppTypography.bodySmall
                                .copyWith(color: AppColors.textLight),
                            prefixIcon: IconButton(
                              icon: const Icon(Icons.search_rounded,
                                  color: AppColors.primary, size: 20),
                              onPressed: () => _loadProperties(page: 1),
                            ),
                            suffixIcon: _searchController.text.isNotEmpty
                                ? IconButton(
                                    icon: const Icon(Icons.clear_rounded,
                                        size: 16),
                                    onPressed: () {
                                      _searchController.clear();
                                      _loadProperties(page: 1);
                                    },
                                  )
                                : null,
                            border: InputBorder.none,
                            enabledBorder: InputBorder.none,
                            focusedBorder: InputBorder.none,
                            contentPadding: const EdgeInsets.symmetric(
                                horizontal: 12, vertical: 12),
                          ),
                        ),
                      ),
                    ),
                    const SizedBox(width: 10),

                    // Map / List Toggle Button
                    InkWell(
                      onTap: () {
                        setState(() => _isMapView = !_isMapView);
                      },
                      borderRadius: BorderRadius.circular(12),
                      child: Container(
                        height: 46,
                        padding: const EdgeInsets.symmetric(horizontal: 12),
                        decoration: BoxDecoration(
                          color: _isMapView
                              ? AppColors.primary
                              : AppColors.primaryLight,
                          borderRadius: BorderRadius.circular(12),
                        ),
                        child: Row(
                          children: [
                            Icon(
                              _isMapView
                                  ? Icons.view_agenda_outlined
                                  : Icons.map_outlined,
                              size: 18,
                              color: _isMapView
                                  ? Colors.white
                                  : AppColors.primary,
                            ),
                            const SizedBox(width: 6),
                            Text(
                              _isMapView ? 'Danh sách' : 'Bản đồ',
                              style: AppTypography.labelSmall.copyWith(
                                color: _isMapView
                                    ? Colors.white
                                    : AppColors.primary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 10),

                // District Chips
                SingleChildScrollView(
                  scrollDirection: Axis.horizontal,
                  child: Row(
                    children: _districts.map((district) {
                      final isSelected = _selectedDistrict == district;
                      return GestureDetector(
                        onTap: () {
                          if (_selectedDistrict != district) {
                            setState(() => _selectedDistrict = district);
                            _loadProperties(page: 1);
                          }
                        },
                        child: Container(
                          margin: const EdgeInsets.only(right: 8),
                          padding: const EdgeInsets.symmetric(
                              horizontal: 14, vertical: 6),
                          decoration: BoxDecoration(
                            color: isSelected
                                ? AppColors.primary
                                : AppColors.surfaceVariant,
                            borderRadius: BorderRadius.circular(999),
                          ),
                          child: Text(
                            district,
                            style: AppTypography.labelSmall.copyWith(
                              color: isSelected
                                  ? Colors.white
                                  : AppColors.textSecondary,
                            ),
                          ),
                        ),
                      );
                    }).toList(),
                  ),
                ),
              ],
            ),
          ),
          const Divider(height: 1),

          // Main View (List or Map)
          Expanded(
            child: _isLoading
                ? const Center(
                    child: CircularProgressIndicator(color: AppColors.primary),
                  )
                : _isMapView
                    ? ExploreMapView(
                        properties: _properties,
                        onPropertySelected: _navigateToDetail,
                      )
                    : RefreshIndicator(
                        onRefresh: () => _loadProperties(page: _currentPage),
                        color: AppColors.primary,
                        child: _properties.isEmpty
                            ? Center(
                                child: SingleChildScrollView(
                                  physics: const AlwaysScrollableScrollPhysics(),
                                  child: Column(
                                    mainAxisSize: MainAxisSize.min,
                                    children: [
                                      const Icon(Icons.search_off_rounded,
                                          size: 56, color: AppColors.textMuted),
                                      const SizedBox(height: 12),
                                      Text('Không tìm thấy khu trọ phù hợp.',
                                          style: AppTypography.headingSmall),
                                      const SizedBox(height: 4),
                                      Text(
                                        'Thử tìm kiếm với từ khóa hoặc quận khác.',
                                        style: AppTypography.bodySmall,
                                      ),
                                    ],
                                  ),
                                ),
                              )
                            : ListView.builder(
                                controller: _scrollController,
                                padding: const EdgeInsets.all(16),
                                itemCount: _properties.length + 1, // +1 cho pagination bar
                                itemBuilder: (context, index) {
                                  if (index < _properties.length) {
                                    final prop = _properties[index];
                                    return Padding(
                                      padding: const EdgeInsets.only(bottom: 16),
                                      child: PropertyCardWidget(
                                        property: prop,
                                        onTap: () => _navigateToDetail(prop),
                                      ),
                                    );
                                  }

                                  // Pagination bar ở cuối danh sách
                                  return _buildPaginationBar(totalPages);
                                },
                              ),
                      ),
          ),
        ],
      ),
    );
  }

  Widget _buildPaginationBar(int totalPages) {
    return Container(
      margin: const EdgeInsets.only(top: 8, bottom: 24),
      padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 12),
      decoration: BoxDecoration(
        color: Colors.white,
        borderRadius: BorderRadius.circular(14),
        border: Border.all(color: AppColors.borderLight),
      ),
      child: Column(
        children: [
          Text(
            'Trang $_currentPage / $totalPages (Tổng $_totalCount khu trọ)',
            style: AppTypography.bodySmall.copyWith(
              color: AppColors.textSecondary,
              fontWeight: FontWeight.w500,
            ),
          ),
          const SizedBox(height: 10),
          Row(
            mainAxisAlignment: MainAxisAlignment.center,
            children: [
              // Nút Trang trước
              ElevatedButton.icon(
                icon: const Icon(Icons.chevron_left_rounded, size: 18),
                label: const Text('Trước'),
                style: ElevatedButton.styleFrom(
                  backgroundColor: _currentPage > 1 ? AppColors.primary : Colors.grey.shade200,
                  foregroundColor: _currentPage > 1 ? Colors.white : Colors.grey.shade500,
                  elevation: 0,
                  minimumSize: const Size(90, 38),
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
                onPressed: _currentPage > 1
                    ? () => _loadProperties(page: _currentPage - 1)
                    : null,
              ),
              const SizedBox(width: 12),

              // Hiển thị danh sách số trang
              ...List.generate(totalPages, (i) {
                final pageNum = i + 1;
                // Chỉ hiển thị trang lân cận nếu quá nhiều trang
                if (totalPages > 5 &&
                    (pageNum != 1 &&
                        pageNum != totalPages &&
                        (pageNum < _currentPage - 1 || pageNum > _currentPage + 1))) {
                  if (pageNum == _currentPage - 2 || pageNum == _currentPage + 2) {
                    return const Padding(
                      padding: EdgeInsets.symmetric(horizontal: 4),
                      child: Text('...'),
                    );
                  }
                  return const SizedBox.shrink();
                }

                final isCurrent = pageNum == _currentPage;
                return Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 3),
                  child: InkWell(
                    onTap: isCurrent ? null : () => _loadProperties(page: pageNum),
                    borderRadius: BorderRadius.circular(8),
                    child: Container(
                      width: 34,
                      height: 34,
                      alignment: Alignment.center,
                      decoration: BoxDecoration(
                        color: isCurrent ? AppColors.primary : AppColors.surfaceVariant,
                        borderRadius: BorderRadius.circular(8),
                      ),
                      child: Text(
                        '$pageNum',
                        style: TextStyle(
                          color: isCurrent ? Colors.white : AppColors.textPrimary,
                          fontWeight: isCurrent ? FontWeight.bold : FontWeight.normal,
                          fontSize: 13,
                        ),
                      ),
                    ),
                  ),
                );
              }),

              const SizedBox(width: 12),
              // Nút Trang sau
              ElevatedButton.icon(
                label: const Text('Sau'),
                icon: const Icon(Icons.chevron_right_rounded, size: 18),
                style: ElevatedButton.styleFrom(
                  backgroundColor: _currentPage < totalPages ? AppColors.primary : Colors.grey.shade200,
                  foregroundColor: _currentPage < totalPages ? Colors.white : Colors.grey.shade500,
                  elevation: 0,
                  minimumSize: const Size(90, 38),
                  padding: const EdgeInsets.symmetric(horizontal: 12),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(10)),
                ),
                onPressed: _currentPage < totalPages
                    ? () => _loadProperties(page: _currentPage + 1)
                    : null,
              ),
            ],
          ),
        ],
      ),
    );
  }
}
