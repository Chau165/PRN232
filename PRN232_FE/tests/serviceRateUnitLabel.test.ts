import { describe, expect, it } from "vitest"
import { serviceRateUnitLabel } from "@/utils/publicFormatting"

describe("serviceRateUnitLabel", () => {
  it.each([
    ["kwh", "usage", "kWh"],
    ["m3", "usage", "m³"],
    ["room_month", "fixed_room", "phòng/tháng"],
    ["person_month", "occupant", "người/tháng"],
    ["vehicle_month", "vehicle", "xe/tháng"],
  ])("labels %s / %s as %s", (unitCode, billingBasis, label) => {
    expect(serviceRateUnitLabel(unitCode, billingBasis)).toBe(label)
  })

  // Dữ liệu thật trên Supabase có dòng "Người ở thêm" lưu billingBasis này.
  it("labels the per-extra-occupant rate as người/tháng", () => {
    expect(serviceRateUnitLabel("person_month", "additional_occupant")).toBe("người/tháng")
  })

  it("returns null for a combination it does not know", () => {
    expect(serviceRateUnitLabel("custom", "custom")).toBeNull()
  })
})
