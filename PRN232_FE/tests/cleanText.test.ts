import { expect, it } from "vitest"
import { cleanText } from "@/utils/cleanText"
it("composes decomposed Vietnamese and collapses whitespace", () => {
  expect(cleanText("  Nguyễn   Xiển ")).toBe("Nguyễn Xiển")
})
