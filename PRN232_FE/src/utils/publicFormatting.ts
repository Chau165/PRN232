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

const normalizeAddressPart = (value: string) =>
  value.normalize("NFC").toLocaleLowerCase("vi").replace(/\s+/g, " ").trim()

// Properties store the full address in addressText for some rows and only the street for others.
// Append ward/district/city only when a comma-separated segment does not already hold them
// (as the whole segment, or at its end, e.g. "Phường Bàn Cờ" covers "Bàn Cờ").
export function formatAddress(
  addressText: string | null | undefined,
  ...parts: Array<string | null | undefined>
): string {
  const segments: string[] = []
  const covered = (part: string) =>
    segments.some((segment) =>
      segment.split(",").map(normalizeAddressPart).some((existing) =>
        existing === part || existing.endsWith(` ${part}`)))

  for (const value of [addressText, ...parts]) {
    const trimmed = value?.trim()
    if (!trimmed || covered(normalizeAddressPart(trimmed))) continue
    segments.push(trimmed)
  }
  return segments.join(", ")
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
