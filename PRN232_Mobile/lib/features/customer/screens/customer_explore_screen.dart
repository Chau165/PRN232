import 'package:flutter/material.dart';
import 'package:go_router/go_router.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
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
  final TextEditingController _searchController = TextEditingController();
  bool _isMapView = false;
  String _selectedDistrict = 'Tất cả';

  final List<String> _districts = [
    'Tất cả',
    'Gò Vấp',
    'Bình Thạnh',
    'TP. Thủ Đức',
    'Quận 7',
    'Dưới 3 triệu',
  ];

  @override
  void dispose() {
    _searchController.dispose();
    super.dispose();
  }

  List<PropertyItem> get _filteredProperties {
    return CustomerMockData.properties.where((p) {
      final matchesQuery = _searchController.text.trim().isEmpty ||
          p.propertyName
              .toLowerCase()
              .contains(_searchController.text.trim().toLowerCase()) ||
          p.district
              .toLowerCase()
              .contains(_searchController.text.trim().toLowerCase());

      final matchesDistrict = _selectedDistrict == 'Tất cả' ||
          (_selectedDistrict == 'Dưới 3 triệu'
              ? p.startingPrice <= 3000000
              : p.district.contains(_selectedDistrict));

      return matchesQuery && matchesDistrict;
    }).toList();
  }

  void _navigateToDetail(PropertyItem property) {
    context.push('/property-detail', extra: property);
  }

  @override
  Widget build(BuildContext context) {
    final properties = _filteredProperties;

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
                          onChanged: (_) => setState(() {}),
                          style: AppTypography.bodyMedium,
                          decoration: InputDecoration(
                            hintText: 'Tìm theo quận, tên đường, khu trọ...',
                            hintStyle: AppTypography.bodySmall
                                .copyWith(color: AppColors.textLight),
                            prefixIcon: const Icon(Icons.search_rounded,
                                color: AppColors.primary, size: 20),
                            suffixIcon: _searchController.text.isNotEmpty
                                ? IconButton(
                                    icon: const Icon(Icons.clear_rounded,
                                        size: 16),
                                    onPressed: () {
                                      _searchController.clear();
                                      setState(() {});
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
                          setState(() => _selectedDistrict = district);
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
            child: _isMapView
                ? ExploreMapView(
                    properties: properties,
                    onPropertySelected: _navigateToDetail,
                  )
                : properties.isEmpty
                    ? Center(
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            const Icon(Icons.search_off_rounded,
                                size: 56, color: AppColors.textMuted),
                            const SizedBox(height: 12),
                            Text('Không tìm thấy khu trọ phù hợp.',
                                style: AppTypography.headingSmall),
                            const SizedBox(height: 4),
                            Text('Thử tìm kiếm với từ khóa hoặc quận khác.',
                                style: AppTypography.bodySmall),
                          ],
                        ),
                      )
                    : ListView.separated(
                        padding: const EdgeInsets.all(16),
                        itemCount: properties.length,
                        separatorBuilder: (_, _) =>
                            const SizedBox(height: 16),
                        itemBuilder: (context, index) {
                          final prop = properties[index];
                          return PropertyCardWidget(
                            property: prop,
                            onTap: () => _navigateToDetail(prop),
                          );
                        },
                      ),
          ),
        ],
      ),
    );
  }
}
