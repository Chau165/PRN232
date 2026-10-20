export function formatVnd(value: number | null | undefined): string {
  if (value === null || value === undefined || !Number.isFinite(value)) {
    return "Chưa có giá"
  }

  return new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 2,
  }).format(value)
}

export function serviceRateUnitLabel(
  unitCode: string,
  billingBasis: string,
): string | null {
  const unitLabels: Record<string, string> = {
    "kwh:usage": "kWh",
    "m3:usage": "m³",
    "room_month:fixed_room": "phòng/tháng",
    "person_month:occupant": "người/tháng",
    "vehicle_month:vehicle": "xe/tháng",
  }

  return unitLabels[`${unitCode}:${billingBasis}`] ?? null
}
