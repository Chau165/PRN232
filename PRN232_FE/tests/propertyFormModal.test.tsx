import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import PropertyFormModal from "@/components/management/PropertyFormModal"
import type { PropertyWriteRequest } from "@/services/propertyService"

const serviceMocks = vi.hoisted(() => ({
  getAdministrativeProvinces: vi.fn(),
  getAdministrativeWards: vi.fn(),
}))

vi.mock("@/services/propertyService", () => serviceMocks)

async function chooseOption(label: string, option: string) {
  const input = screen.getByRole("combobox", { name: label })
  fireEvent.focus(input)
  fireEvent.change(input, { target: { value: option } })
  fireEvent.click(await screen.findByRole("option", { name: option }))
}

describe("PropertyFormModal address cascade", () => {
  beforeEach(() => {
    vi.clearAllMocks()
    serviceMocks.getAdministrativeProvinces.mockResolvedValue([
      { provinceCode: "01", provinceName: "Thành phố Hà Nội" },
      { provinceCode: "79", provinceName: "Thành phố Hồ Chí Minh" },
    ])
    serviceMocks.getAdministrativeWards.mockImplementation(
      async (_accessToken: string, provinceCode: string) =>
        provinceCode === "01"
          ? [{ wardCode: "00004", wardName: "Phường Ba Đình" }]
          : [{ wardCode: "ward-79", wardName: "Phường Bến Thành" }],
    )
  })

  it("loads wards by province and submits without a map confirmation step", async () => {
    const onSubmit = vi.fn(async (_request: PropertyWriteRequest) => true)
    render(
      <PropertyFormModal
        mode="create"
        accessToken="admin-token"
        saving={false}
        onClose={vi.fn()}
        onSubmit={onSubmit}
      />,
    )

    const wardInput = screen.getByRole("combobox", { name: "Phường/Xã" })
    expect((wardInput as HTMLInputElement).disabled).toBe(true)
    expect(screen.getByText("Chọn tỉnh/thành phố trước.")).toBeTruthy()
    expect(screen.queryByText("Bản đồ xác nhận vị trí")).toBeNull()
    expect(screen.queryByRole("button", { name: "Kiểm tra vị trí" })).toBeNull()

    await chooseOption("Tỉnh/Thành phố", "Thành phố Hà Nội")
    await waitFor(() =>
      expect(serviceMocks.getAdministrativeWards).toHaveBeenCalledWith(
        "admin-token",
        "01",
        expect.any(AbortSignal),
      ),
    )
    await chooseOption("Phường/Xã", "Phường Ba Đình")
    fireEvent.change(screen.getByLabelText("Tên khu trọ"), {
      target: { value: "Nhà trọ Bình An" },
    })
    fireEvent.change(screen.getByLabelText("Số nhà, tên đường"), {
      target: { value: "12 đường Lê Lợi" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo khu trọ" }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))
    expect(onSubmit).toHaveBeenCalledWith(expect.objectContaining({
      propertyName: "Nhà trọ Bình An",
      addressText: "12 đường Lê Lợi",
      provinceCode: "01",
      wardCode: "00004",
      city: "Thành phố Hà Nội",
      ward: "Phường Ba Đình",
    }))
    const submitted = onSubmit.mock.calls[0][0]
    expect(submitted).not.toHaveProperty("confirmedLatitude")
    expect(submitted).not.toHaveProperty("confirmedLongitude")
  })
})
