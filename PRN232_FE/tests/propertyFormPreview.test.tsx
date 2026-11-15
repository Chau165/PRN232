import { afterEach, expect, it, vi } from "vitest"
import { cleanup, fireEvent, render, screen } from "@testing-library/react"
import PropertyFormModal from "@/components/management/PropertyFormModal"

vi.mock("@/services/propertyService", () => ({
  getAdministrativeProvinces: vi.fn(async () => []),
  getAdministrativeWards: vi.fn(async () => []),
}))

afterEach(() => { cleanup(); vi.unstubAllGlobals() })

it("previews selected files, closes the enlarged image without closing the form, and releases object URLs", () => {
  const create = vi.fn().mockReturnValueOnce("blob:first").mockReturnValueOnce("blob:second")
  const revoke = vi.fn()
  const OriginalURL = URL
  vi.stubGlobal("URL", class extends OriginalURL {
    static createObjectURL = create
    static revokeObjectURL = revoke
  })
  const closeForm = vi.fn()
  const view = render(<PropertyFormModal mode="create" accessToken="test" saving={false} onClose={closeForm} onSubmit={vi.fn()} />)
  const input = screen.getByLabelText("Ảnh đại diện")
  fireEvent.change(input, { target: { files: [new File(["first"], "first.png", { type: "image/png" })] } })
  expect(screen.getByAltText("Ảnh đại diện xem trước").getAttribute("src")).toBe("blob:first")
  fireEvent.click(screen.getByRole("button", { name: "Xem ảnh đại diện phóng to" }))
  expect(screen.getByRole("dialog", { name: "Ảnh đại diện phóng to" })).toBeTruthy()
  fireEvent.keyDown(document, { key: "Escape" })
  expect(screen.queryByRole("dialog", { name: "Ảnh đại diện phóng to" })).toBeNull()
  expect(closeForm).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole("button", { name: "Xem ảnh đại diện phóng to" }))
  fireEvent.click(screen.getByRole("button", { name: "Đóng ảnh phóng to" }))
  fireEvent.change(input, { target: { files: [new File(["second"], "second.png", { type: "image/png" })] } })
  expect(revoke).toHaveBeenCalledWith("blob:first")
  expect(screen.getByAltText("Ảnh đại diện xem trước").getAttribute("src")).toBe("blob:second")
  view.unmount()
  expect(revoke).toHaveBeenCalledWith("blob:second")
})

it("renders server errors inside the form below the submit controls", () => {
  render(<PropertyFormModal mode="create" accessToken="test" saving={false} submitError="Không xác định được địa chỉ" onClose={vi.fn()} onSubmit={vi.fn()} />)
  const alert = screen.getByRole("alert")
  expect(alert.textContent).toBe("Không xác định được địa chỉ")
  const form = screen.getByRole("button", { name: "Tạo khu trọ" }).closest("form")!
  expect(form.lastElementChild).toBe(alert)
  expect(alert.closest('[role="dialog"]')).toBeTruthy()
})
