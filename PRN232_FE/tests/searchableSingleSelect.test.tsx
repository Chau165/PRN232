import { fireEvent, render, screen } from "@testing-library/react"
import { describe, expect, it, vi } from "vitest"
import SearchableSingleSelect from "@/components/common/SearchableSingleSelect"

const provinces = [
  { value: "01", label: "Thành phố Hà Nội" },
  { value: "79", label: "Thành phố Hồ Chí Minh" },
]

describe("SearchableSingleSelect", () => {
  it("filters Vietnamese names and selects by keyboard", () => {
    const onChange = vi.fn()
    render(
      <SearchableSingleSelect
        id="province"
        label="Tỉnh/Thành phố"
        options={provinces}
        value=""
        onChange={onChange}
      />,
    )

    const input = screen.getByRole("combobox", { name: "Tỉnh/Thành phố" })
    fireEvent.focus(input)
    fireEvent.change(input, { target: { value: "ho chi minh" } })
    fireEvent.keyDown(input, { key: "ArrowDown" })
    fireEvent.keyDown(input, { key: "Enter" })

    expect(onChange).toHaveBeenCalledWith("79")
    expect(screen.queryByRole("listbox")).toBeNull()
  })

  it("closes the options on Escape and reports a disabled hint", () => {
    render(
      <SearchableSingleSelect
        id="ward"
        label="Phường/Xã"
        options={[]}
        value=""
        onChange={vi.fn()}
        disabled
        hint="Chọn tỉnh/thành phố trước."
      />,
    )

    expect(screen.getByRole("combobox", { name: "Phường/Xã" }).hasAttribute("disabled")).toBe(true)
    expect(screen.getByText("Chọn tỉnh/thành phố trước.")).toBeTruthy()
  })
})
