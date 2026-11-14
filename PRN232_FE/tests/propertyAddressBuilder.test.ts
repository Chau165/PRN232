import { describe, expect, it } from "vitest"
import {
  buildFullPropertyAddress,
  removeKnownAdministrativeSegments,
} from "@/utils/propertyAddress"

describe("property address normalization", () => {
  it("builds a full address without repeating the selected ward or province", () => {
    expect(
      buildFullPropertyAddress(
        "12 đường Lê Lợi",
        "Phường Bến Thành",
        "Thành phố Hồ Chí Minh",
      ),
    ).toBe("12 đường Lê Lợi, Phường Bến Thành, Thành phố Hồ Chí Minh, Việt Nam")
  })

  it("removes known legacy administrative segments only when normalizing", () => {
    expect(
      removeKnownAdministrativeSegments(
        "12 đường Lê Lợi, Phường Bến Thành, Khu vực Quận 1 cũ, TP. Hồ Chí Minh",
        ["Bến Thành", "Khu vực Quận 1 cũ", "Thành phố Hồ Chí Minh"],
      ),
    ).toBe("12 đường Lê Lợi")
  })
})
