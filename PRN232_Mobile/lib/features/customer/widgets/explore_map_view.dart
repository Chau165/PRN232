import 'package:flutter/material.dart';
import 'package:flutter_map/flutter_map.dart';
import 'package:latlong2/latlong.dart';
import '../../../core/constants/app_colors.dart';
import '../../../core/constants/app_typography.dart';
import '../../../core/utils/formatters.dart';
import '../../../core/widgets/app_card.dart';
import '../../../core/widgets/status_badge.dart';
import '../models/customer_models.dart';

class ExploreMapView extends StatefulWidget {
  final List<PropertyItem> properties;
  final ValueChanged<PropertyItem> onPropertySelected;

  const ExploreMapView({
    super.key,
    required this.properties,
    required this.onPropertySelected,
  });

  @override
  State<ExploreMapView> createState() => _ExploreMapViewState();
}

class _ExploreMapViewState extends State<ExploreMapView> {
  final MapController _mapController = MapController();
  PropertyItem? _selectedProperty;

  @override
  void initState() {
    super.initState();
    if (widget.properties.isNotEmpty) {
      _selectedProperty = widget.properties.first;
    }
  }

  void _onMarkerTap(PropertyItem prop) {
    setState(() {
      _selectedProperty = prop;
    });
    _mapController.move(prop.location, 14.5);
  }

  @override
  Widget build(BuildContext context) {
    final initialCenter = widget.properties.isNotEmpty
        ? widget.properties.first.location
        : const LatLng(10.8231, 106.6896);

    return Stack(
      children: [
        // OpenStreetMap via flutter_map
        FlutterMap(
          mapController: _mapController,
          options: MapOptions(
            initialCenter: initialCenter,
            initialZoom: 13.0,
            minZoom: 10.0,
            maxZoom: 18.0,
          ),
          children: [
            TileLayer(
              urlTemplate: 'https://tile.openstreetmap.org/{z}/{x}/{y}.png',
              userAgentPackageName: 'com.prn232.mobile',
            ),
            MarkerLayer(
              markers: widget.properties.map((prop) {
                final isSelected =
                    _selectedProperty?.propertyId == prop.propertyId;

                return Marker(
                  point: prop.location,
                  width: 130,
                  height: 52,
                  child: GestureDetector(
                    onTap: () => _onMarkerTap(prop),
                    child: AnimatedContainer(
                      duration: const Duration(milliseconds: 200),
                      padding: const EdgeInsets.symmetric(
                          horizontal: 8, vertical: 4),
                      decoration: BoxDecoration(
                        color: isSelected ? AppColors.primary : Colors.white,
                        borderRadius: BorderRadius.circular(12),
                        border: Border.all(
                          color: isSelected ? Colors.white : AppColors.primary,
                          width: 2,
                        ),
                        boxShadow: [
                          BoxShadow(
                            color: Colors.black.withValues(alpha: 0.18),
                            blurRadius: 8,
                            offset: const Offset(0, 3),
                          ),
                        ],
                      ),
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        children: [
                          Text(
                            prop.propertyName,
                            style: AppTypography.labelSmall.copyWith(
                              color: isSelected
                                  ? Colors.white
                                  : AppColors.textPrimary,
                              fontSize: 10,
                              fontWeight: FontWeight.bold,
                            ),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          Text(
                            Formatters.formatShortPrice(prop.startingPrice),
                            style: AppTypography.labelSmall.copyWith(
                              color: isSelected
                                  ? AppColors.primaryLight
                                  : AppColors.primary,
                              fontSize: 11,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                );
              }).toList(),
            ),
          ],
        ),

        // Controls overlay: Recenter button
        Positioned(
          top: 16,
          right: 16,
          child: FloatingActionButton.small(
            backgroundColor: Colors.white,
            foregroundColor: AppColors.primary,
            elevation: 3,
            onPressed: () {
              if (_selectedProperty != null) {
                _mapController.move(_selectedProperty!.location, 14.5);
              } else {
                _mapController.move(const LatLng(10.8231, 106.6896), 13.0);
              }
            },
            child: const Icon(Icons.my_location_rounded),
          ),
        ),

        // Selected Property Floating Preview Card
        if (_selectedProperty != null)
          Positioned(
            left: 16,
            right: 16,
            bottom: 20,
            child: GestureDetector(
              onTap: () => widget.onPropertySelected(_selectedProperty!),
              child: AppCard(
                padding: const EdgeInsets.all(12),
                elevation: 6,
                border: const BorderSide(color: Color(0xFFC7E7E2), width: 1.5),
                child: Row(
                  children: [
                    ClipRRect(
                      borderRadius: BorderRadius.circular(10),
                      child: Container(
                        width: 80,
                        height: 80,
                        color: AppColors.primaryLight,
                        child: _selectedProperty!.primaryImageUrl != null
                            ? Image.network(
                                _selectedProperty!.primaryImageUrl!,
                                fit: BoxFit.cover,
                                errorBuilder: (_, _, _) => const Icon(
                                  Icons.apartment_rounded,
                                  color: AppColors.primary,
                                  size: 32,
                                ),
                              )
                            : const Icon(Icons.apartment_rounded,
                                color: AppColors.primary, size: 32),
                      ),
                    ),
                    const SizedBox(width: 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Expanded(
                                child: Text(
                                  _selectedProperty!.propertyName,
                                  style: AppTypography.headingSmall,
                                  maxLines: 1,
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                              StatusBadge(
                                label:
                                    '${_selectedProperty!.availableRoomCount} phòng',
                                tone: StatusTone.green,
                              ),
                            ],
                          ),
                          const SizedBox(height: 2),
                          Text(
                            _selectedProperty!.fullAddress,
                            style: AppTypography.bodySmall,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                          ),
                          const SizedBox(height: 6),
                          Row(
                            mainAxisAlignment: MainAxisAlignment.spaceBetween,
                            children: [
                              Text(
                                'Từ ${Formatters.formatShortPrice(_selectedProperty!.startingPrice)}',
                                style: AppTypography.labelLarge
                                    .copyWith(color: AppColors.primary),
                              ),
                              Row(
                                children: [
                                  Text(
                                    'Chi tiết',
                                    style: AppTypography.labelSmall
                                        .copyWith(color: AppColors.primary),
                                  ),
                                  const Icon(Icons.chevron_right,
                                      size: 16, color: AppColors.primary),
                                ],
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}
