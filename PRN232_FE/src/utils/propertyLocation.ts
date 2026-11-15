export type PropertyLocationPoint = { latitude: number; longitude: number }

// Same supported envelope as the write API; this does not verify ward boundaries.
export function isSupportedPropertyLocation(point: PropertyLocationPoint): boolean {
  return Number.isFinite(point.latitude) && Number.isFinite(point.longitude) &&
    point.latitude >= 6.5 && point.latitude <= 24 &&
    point.longitude >= 102 && point.longitude <= 117.5
}
