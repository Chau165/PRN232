import { describe, expect, it } from "vitest"
import { formatAddress } from "@/utils/publicFormatting"

describe("formatAddress", () => {
  it("does not repeat ward, district and city already written in the address text", () => {
    expect(formatAddress("01, Bến Thành, Khu vực Quận 1 cũ, Thành phố Hồ Chí Minh",
      "Bến Thành", "Khu vực Quận 1 cũ", "Thành phố Hồ Chí Minh"))
      .toBe("01, Bến Thành, Khu vực Quận 1 cũ, Thành phố Hồ Chí Minh")
  })

  it("treats a part written with its administrative prefix as already present", () => {
    expect(formatAddress("Phường Bàn Cờ, Khu vực Quận 3 cũ, Thành phố Hồ Chí Minh",
      "Bàn Cờ", "Khu vực Quận 3 cũ", "Thành phố Hồ Chí Minh"))
      .toBe("Phường Bàn Cờ, Khu vực Quận 3 cũ, Thành phố Hồ Chí Minh")
  })

  it("appends the parts a street-only address is missing", () => {
    expect(formatAddress("12 Nguyễn Trãi", "Bến Thành", "Quận 1", "TP. Hồ Chí Minh"))
      .toBe("12 Nguyễn Trãi, Bến Thành, Quận 1, TP. Hồ Chí Minh")
  })

  it("does not mistake a word inside the street name for the ward", () => {
    expect(formatAddress("5 An Dương Vương", "An", "Quận 5", "TP. Hồ Chí Minh"))
      .toBe("5 An Dương Vương, An, Quận 5, TP. Hồ Chí Minh")
  })

  it("ignores empty parts and compares without case", () => {
    expect(formatAddress("  ", null, "quận 1", undefined)).toBe("quận 1")
    expect(formatAddress("10 Lê Lợi, Quận 1", "", "QUẬN 1", "")).toBe("10 Lê Lợi, Quận 1")
  })
})
