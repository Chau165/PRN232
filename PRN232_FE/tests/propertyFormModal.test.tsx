import { fireEvent, render, screen, waitFor } from "@testing-library/react"

import { beforeEach, describe, expect, it, vi } from "vitest"

import PropertyFormModal from "@/components/management/PropertyFormModal"

import type { PropertyWriteRequest } from "@/services/propertyService"

const serviceMocks = vi.hoisted(() => ({
  getAdministrativeProvinces: vi.fn(),

  getAdministrativeWards: vi.fn(),
}))

vi.mock("@/services/propertyService", () => serviceMocks)
vi.mock("@/components/management/PropertyLocationPicker", () => ({
  default: ({ onChange }: { onChange: (p: unknown) => void }) => (
    <button
      type="button"
      onClick={() =>
        onChange({
          latitude:21.032, longitude:105.814,
        })
      }
    >
      Chọn ghim kiểm thử
    </button>
  ),
}))

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
          : [
              { wardCode: "ward-79", wardName: "Phường Bến Thành" },

              { wardCode: "27367", wardName: "Phường Bình Tây" },
            ],
    )
  })

  it("loads wards by province and requires map coordinates for a new address", async () => {
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
    expect(onSubmit).not.toHaveBeenCalled()
    expect(
      screen.getByText("Hãy chọn ghim khu trọ trên bản đồ trước khi lưu."),
    ).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Chọn ghim kiểm thử" }))
    fireEvent.click(screen.getByRole("button", { name: "Tạo khu trọ" }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        propertyName: "Nhà trọ Bình An",

        addressText: "12 đường Lê Lợi",

        provinceCode: "01",

        wardCode: "00004",
      }),
    )

    const submitted = onSubmit.mock.calls[0][0]

    expect(submitted).not.toHaveProperty("ward")

    expect(submitted).not.toHaveProperty("district")

    expect(submitted).not.toHaveProperty("city")

    expect(submitted).not.toHaveProperty("confirmedLatitude")

    expect(submitted).not.toHaveProperty("confirmedLongitude")
    expect(submitted.latitude).toBe(21.032)
    expect(submitted.longitude).toBe(105.814)
    fireEvent.change(screen.getByLabelText("Số nhà, tên đường"), {
      target: { value: "14 đường Lê Lợi" },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tạo khu trọ" }))
    expect(onSubmit).toHaveBeenCalledTimes(1)
    expect(
      screen.getByText("Hãy chọn ghim khu trọ trên bản đồ trước khi lưu."),
    ).toBeTruthy()
  })

  it("preselects province and ward from initialProperty codes in edit mode and submits only codes", async () => {
    const onSubmit = vi.fn(async (_request: PropertyWriteRequest) => true)

    render(
      <PropertyFormModal
        mode="edit"
        accessToken="admin-token"
        initialProperty={{
          propertyId: 21,

          ownerId: 1,

          ownerName: "Admin",

          propertyName: "Khu trọ An Khang",

          addressText: "12 đường Lê Lợi",

          ward: "Tên phường cũ khác danh mục",

          district: "Quận cũ",

          city: "Tên tỉnh cũ khác danh mục",

          provinceCode: "01",

          wardCode: "00004",

          roomCount: 4,

          description: "Khu trọ yên tĩnh",
        }}
        saving={false}
        onClose={vi.fn()}
        onSubmit={onSubmit}
      />,
    )

    expect(
      screen.queryByRole("button", { name: "Chuẩn hóa địa chỉ" }),
    ).toBeNull()

    await waitFor(() =>
      expect(serviceMocks.getAdministrativeWards).toHaveBeenCalledWith(
        "admin-token",

        "01",

        expect.anything(),
      ),
    )

    const provinceSelect = screen.getByRole("combobox", {
      name: "Tỉnh/Thành phố",
    }) as HTMLInputElement

    const wardSelect = screen.getByRole("combobox", {
      name: "Phường/Xã",
    }) as HTMLInputElement

    await waitFor(() => {
      expect(provinceSelect.value).toBe("Thành phố Hà Nội")

      expect(wardSelect.value).toBe("Phường Ba Đình")
    })

    fireEvent.change(screen.getByLabelText("Tên khu trọ"), {
      target: { value: "Khu trọ An Khang Mới" },
    })

    fireEvent.click(screen.getByRole("button", { name: "Lưu khu trọ" }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))

    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({
        propertyName: "Khu trọ An Khang Mới",

        addressText: "12 đường Lê Lợi",

        provinceCode: "01",

        wardCode: "00004",

        description: "Khu trọ yên tĩnh",
      }),
    )

    const submitted = onSubmit.mock.calls[0][0]

    expect(submitted).not.toHaveProperty("ward")

    expect(submitted).not.toHaveProperty("district")

    expect(submitted).not.toHaveProperty("city")
  })

  it("allows editing property name when legacy addressText is unchanged even if it contains ward or city names", async () => {
    const onSubmit = vi.fn(async (_request: PropertyWriteRequest) => true)

    render(
      <PropertyFormModal
        mode="edit"
        accessToken="admin-token"
        initialProperty={{
          propertyId: 25,

          ownerId: 1,

          ownerName: "Admin",

          propertyName: "Nhà trọ Bình Tây Cũ",

          addressText: "Bình Tây, Khu vực Quận 6, Thành phố Hồ Chí Minh",

          ward: "Phường Bình Tây",

          district: "Quận 6",

          city: "Thành phố Hồ Chí Minh",

          provinceCode: "79",

          wardCode: "27367",

          roomCount: 8,

          description: "Khu trọ quận 6 cũ",
        }}
        saving={false}
        onClose={vi.fn()}
        onSubmit={onSubmit}
      />,
    )

    await waitFor(() =>
      expect(serviceMocks.getAdministrativeWards).toHaveBeenCalledWith(
        "admin-token",

        "79",

        expect.anything(),
      ),
    )

    const provinceSelect = screen.getByRole("combobox", {
      name: "Tỉnh/Thành phố",
    }) as HTMLInputElement

    const wardSelect = screen.getByRole("combobox", {
      name: "Phường/Xã",
    }) as HTMLInputElement

    await waitFor(() => {
      expect(provinceSelect.value).toBe("Thành phố Hồ Chí Minh")

      expect(wardSelect.value).toBe("Phường Bình Tây")
    })

    fireEvent.change(screen.getByLabelText("Tên khu trọ"), {
      target: { value: "Nhà trọ Bình Tây Mới" },
    })

    fireEvent.click(screen.getByRole("button", { name: "Lưu khu trọ" }))

    await waitFor(() => expect(onSubmit).toHaveBeenCalledTimes(1))

    expect(onSubmit).toHaveBeenCalledWith({
      propertyName: "Nhà trọ Bình Tây Mới",

      addressText: "Bình Tây, Khu vực Quận 6, Thành phố Hồ Chí Minh",

      provinceCode: "79",

      wardCode: "27367",

      primaryImage: undefined,

      description: "Khu trọ quận 6 cũ",
    })
  })

  it("blocks invalid propertyName, addressText, and description early and maps backend validationErrors to fields", async () => {
    const onSubmit = vi.fn(async (_request: PropertyWriteRequest) => true)

    const { rerender } = render(
      <PropertyFormModal
        mode="create"
        accessToken="admin-token"
        saving={false}
        onClose={vi.fn()}
        onSubmit={onSubmit}
      />,
    )

    await chooseOption("Tỉnh/Thành phố", "Thành phố Hà Nội")

    await waitFor(() =>
      expect(serviceMocks.getAdministrativeWards).toHaveBeenCalled(),
    )

    await chooseOption("Phường/Xã", "Phường Ba Đình")

    fireEvent.change(screen.getByLabelText("Tên khu trọ"), {
      target: { value: "AB" },
    })

    fireEvent.change(screen.getByLabelText("Số nhà, tên đường"), {
      target: { value: "1234" },
    })

    fireEvent.change(screen.getByLabelText("Mô tả (không bắt buộc)"), {
      target: { value: "a".repeat(2001) },
    })

    fireEvent.click(screen.getByRole("button", { name: "Tạo khu trọ" }))

    expect(
      screen.getByText("Tên khu trọ phải từ 3 đến 150 ký tự."),
    ).toBeTruthy()

    expect(
      screen.getByText("Số nhà, tên đường phải từ 5 đến 255 ký tự."),
    ).toBeTruthy()

    expect(screen.getByText("Mô tả tối đa 2000 ký tự.")).toBeTruthy()

    expect(onSubmit).not.toHaveBeenCalled()

    const { ApiError } = await import("@/api/apiClient")

    rerender(
      <PropertyFormModal
        mode="create"
        accessToken="admin-token"
        saving={false}
        serverError={
          new ApiError("Dữ liệu không hợp lệ.", {
            status: 400,

            validationErrors: {
              propertyName: ["Tên khu trọ phải từ 3 đến 150 ký tự."],

              unmappedField: ["Lỗi ngoài ô nhập."],
            },
          })
        }
        onClose={vi.fn()}
        onSubmit={onSubmit}
      />,
    )

    expect(
      await screen.findByText("Tên khu trọ phải từ 3 đến 150 ký tự."),
    ).toBeTruthy()

    expect(screen.getByText("Lỗi ngoài ô nhập.")).toBeTruthy()
  })
})
