import { fireEvent, render, screen } from "@testing-library/react"
import { useState } from "react"
import { describe, expect, it, vi } from "vitest"
import AmenityMultiSelect from "@/components/common/AmenityMultiSelect"
import { PublicApiError } from "@/services/publicBrowsingService"
import type { PublicResourceState } from "@/hooks/usePublicApiResource"
import type { PublicAmenitySummary } from "@/types/publicBrowsing"

const availableAmenities = [
  { amenityId: 31, name: "Wi-Fi" },
  { amenityId: 42, name: "Máy lạnh" },
  { amenityId: 57, name: "Ban công" },
]

function setup(initialIds: number[] = [], resource: PublicResourceState<PublicAmenitySummary[]> = { status: "success", data: availableAmenities }) {
  const onChange = vi.fn()
  const retry = vi.fn()
  function Harness() {
    const [selectedAmenityIds, setSelectedAmenityIds] = useState(initialIds)
    return <>
      <AmenityMultiSelect resource={{ ...resource, retry }} selectedAmenityIds={selectedAmenityIds} onChange={(ids) => { onChange(ids); setSelectedAmenityIds(ids) }} />
      <button type="button">Bên ngoài</button>
    </>
  }
  const view = render(<Harness />)
  const trigger = screen.getByRole("combobox", { name: "Tiện ích mong muốn" })
  return { ...view, trigger, onChange, retry }
}

describe("searchable amenity multi-select", () => {
  it("starts closed, opens a dropdown and searches Vietnamese names without accents", () => {
    const { trigger } = setup()
    expect(trigger.getAttribute("aria-expanded")).toBe("false")
    expect(screen.queryByRole("checkbox")).toBeNull()
    fireEvent.click(trigger)
    const search = screen.getByRole("searchbox", { name: "Tìm tiện ích" })
    expect(document.activeElement).toBe(search)
    fireEvent.change(search, { target: { value: "MAY LANH" } })
    expect(screen.getByRole("checkbox", { name: "Máy lạnh" })).toBeTruthy()
    expect(screen.queryByRole("checkbox", { name: "Wi-Fi" })).toBeNull()
    fireEvent.keyDown(search, { key: "Enter" })
    expect(screen.getByRole("button", { name: "Bỏ tiện ích Máy lạnh" })).toBeTruthy()
    fireEvent.change(search, { target: { value: "không tồn tại" } })
    expect(screen.getByText("Không tìm thấy tiện ích phù hợp.")).toBeTruthy()
  })

  it("selects unique numeric IDs, renders named removable chips, and clears all", () => {
    const { trigger, onChange } = setup()
    fireEvent.click(trigger)
    fireEvent.click(screen.getByRole("checkbox", { name: "Wi-Fi" }))
    fireEvent.click(screen.getByRole("checkbox", { name: "Máy lạnh" }))
    expect(onChange).toHaveBeenLastCalledWith([31, 42])
    expect(screen.getByRole("button", { name: "Bỏ tiện ích Wi-Fi" })).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Bỏ tiện ích Wi-Fi" }))
    expect(onChange).toHaveBeenLastCalledWith([42])
    expect(trigger.getAttribute("aria-expanded")).toBe("true")
    fireEvent.click(screen.getByRole("button", { name: "Xóa lựa chọn" }))
    expect(onChange).toHaveBeenLastCalledWith([])
    expect(screen.queryByRole("button", { name: "Bỏ tiện ích Máy lạnh" })).toBeNull()
  })

  it("supports arrow navigation, Enter selection, Escape and focus return", () => {
    const { trigger, onChange } = setup()
    trigger.focus()
    fireEvent.keyDown(trigger, { key: "ArrowDown" })
    fireEvent.keyDown(screen.getByRole("searchbox"), { key: "ArrowDown" })
    const wifi = screen.getByRole("checkbox", { name: "Wi-Fi" })
    expect(document.activeElement).toBe(wifi)
    fireEvent.keyDown(wifi, { key: "Enter" })
    expect(onChange).toHaveBeenLastCalledWith([31])
    fireEvent.keyDown(wifi, { key: "ArrowDown" })
    expect(document.activeElement).toBe(screen.getByRole("checkbox", { name: "Máy lạnh" }))
    fireEvent.keyDown(document.activeElement!, { key: "Escape" })
    expect(screen.queryByRole("dialog")).toBeNull()
    expect(document.activeElement).toBe(trigger)
  })

  it("closes on outside pointer, focus departure and Done", () => {
    const { trigger } = setup()
    fireEvent.click(trigger)
    fireEvent.pointerDown(screen.getByRole("button", { name: "Bên ngoài" }))
    expect(screen.queryByRole("dialog")).toBeNull()
    fireEvent.click(trigger)
    fireEvent.click(screen.getByRole("button", { name: "Hoàn tất" }))
    expect(document.activeElement).toBe(trigger)
    fireEvent.click(trigger)
    fireEvent.blur(screen.getByRole("searchbox"), { relatedTarget: screen.getByRole("button", { name: "Bên ngoài" }) })
    expect(screen.queryByRole("dialog")).toBeNull()
  })

  it("hydrates valid chips and explicitly removes obsolete IDs without fake options", () => {
    const { onChange } = setup([42, 999])
    expect(screen.getByRole("button", { name: "Bỏ tiện ích Máy lạnh" })).toBeTruthy()
    expect(screen.getByText(/1 tiện ích đã chọn không còn tồn tại/)).toBeTruthy()
    expect(screen.queryByText("999")).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Bỏ lựa chọn lỗi thời" }))
    expect(onChange).toHaveBeenLastCalledWith([42])
  })

  it("shows loading without invented options", () => {
    const { trigger } = setup([], { status: "loading" })
    expect(screen.getByRole("status").textContent).toContain("Đang tải tiện ích")
    fireEvent.click(trigger)
    expect(screen.queryByRole("checkbox")).toBeNull()
  })

  it("shows API errors and offers retry without fallback data", () => {
    const { trigger, retry } = setup([], { status: "error", error: new PublicApiError("network", "offline") })
    expect(screen.getByRole("alert").textContent).toContain("Không thể tải danh sách tiện ích")
    fireEvent.click(screen.getByRole("button", { name: "Thử lại" }))
    expect(retry).toHaveBeenCalledOnce()
    fireEvent.click(trigger)
    expect(screen.queryByRole("checkbox")).toBeNull()
  })

  it("shows an empty catalog and caps selection at the backend limit", () => {
    const empty = setup([], { status: "success", data: [] })
    fireEvent.click(empty.trigger)
    expect(screen.getByText("Hiện chưa có tiện ích nào.")).toBeTruthy()
    empty.unmount()
    const options = Array.from({ length: 21 }, (_, index) => ({ amenityId: index + 1, name: `Tiện ích ${index + 1}` }))
    const { trigger, onChange } = setup(options.slice(0, 20).map((option) => option.amenityId), { status: "success", data: options })
    fireEvent.click(trigger)
    expect((screen.getByRole("checkbox", { name: "Tiện ích 21" }) as HTMLInputElement).disabled).toBe(true)
    fireEvent.click(screen.getByRole("checkbox", { name: "Tiện ích 1" }))
    expect(onChange.mock.lastCall?.[0]).toHaveLength(19)
    expect((screen.getByRole("checkbox", { name: "Tiện ích 21" }) as HTMLInputElement).disabled).toBe(false)
  })
})
