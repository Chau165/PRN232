import { fireEvent, render, screen } from "@testing-library/react"
import { expect, it, vi } from "vitest"
import PropertyFormModal from "@/components/management/PropertyFormModal"
vi.mock("@/components/management/PropertyLocationPicker", () => ({ default: () => null }))
vi.stubGlobal("fetch", vi.fn(async () => Response.json([])))
it("explains the read-only full address and sends the cursor to the street field", () => {
  render(<PropertyFormModal mode="create" accessToken="test" saving={false} onClose={vi.fn()} onSubmit={vi.fn(async () => true)} />)
  const full = screen.getByLabelText("Địa chỉ đầy đủ") as HTMLInputElement
  expect(full.readOnly).toBe(true)
  expect(screen.getByText(/Muốn đổi địa chỉ, sửa ô/)).toBeTruthy()
  fireEvent.click(full)
  expect(document.activeElement).toBe(screen.getByLabelText("Số nhà, tên đường"))
})
