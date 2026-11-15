import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import { ApiError } from "@/api/apiClient"
import PropertyServiceRatesPanel from "@/components/management/PropertyServiceRatesPanel"
import type { PropertyServiceRateDto } from "@/types/serviceRate"

const mocks = vi.hoisted(() => ({ role: "ADMIN", rates: vi.fn(), catalog: vi.fn(), create: vi.fn() }))
vi.mock("@/auth/AuthContext", () => ({ useAuth: () => ({ accessToken: "token", user: { id: "1", role: mocks.role } }) }))
vi.mock("@/services/serviceRateService", () => ({
  getPropertyServiceRates: mocks.rates, getServiceCatalog: mocks.catalog, createPropertyServiceRate: mocks.create,
}))

const rate = (changes: Partial<PropertyServiceRateDto> = {}): PropertyServiceRateDto => ({
  rateId: 1, propertyId: 5, serviceId: 1, serviceName: "Điện", price: 3500, unitCode: "kwh", billingBasis: "usage",
  effectiveFrom: "2020-01-01", effectiveTo: null, ...changes,
})

beforeEach(() => {
  mocks.role = "ADMIN"
  mocks.rates.mockReset().mockResolvedValue([rate(), rate({ rateId: 2, serviceId: 3, serviceName: "Wifi", price: 100000, unitCode: "room_month", billingBasis: "fixed_room", effectiveFrom: "2020-01-01", effectiveTo: "2020-07-01" })])
  mocks.catalog.mockReset().mockResolvedValue([{
    serviceId: 2,
    name: "Nước",
    price: 0,
    defaultUnitCode: "m3",
    defaultBillingBasis: "usage",
  }])
  mocks.create.mockReset().mockResolvedValue(rate({ rateId: 3, serviceId: 2, serviceName: "Nước", price: 18000, unitCode: "m3" }))
})

describe("PropertyServiceRatesPanel", () => {
  it("shows rates with their period and marks the one in effect, read-only for a Manager", async () => {
    mocks.role = "MANAGER"
    render(<PropertyServiceRatesPanel propertyId={5} />)
    expect(await screen.findByText(/3\.500\s₫\/kWh/)).toBeTruthy()
    expect(screen.getByText("Từ 01/2020 · không thời hạn")).toBeTruthy()
    // effectiveTo là mốc loại trừ: 07/2020 nghĩa là áp dụng đến hết 06/2020.
    expect(screen.getByText("Từ 01/2020 đến hết 06/2020")).toBeTruthy()
    expect(screen.getAllByText("Đang áp dụng")).toHaveLength(1)
    expect(screen.queryByRole("button", { name: "Thêm mức giá" })).toBeNull()
    expect(mocks.catalog).not.toHaveBeenCalled()
  })

  it("lets an Admin add a supported rate and reloads the table", async () => {
    render(<PropertyServiceRatesPanel propertyId={5} />)
    await screen.findByRole("option", { name: "Nước" })
    expect(screen.queryByLabelText("Cách tính tiền")).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))
    expect(screen.getByRole("alert").textContent).toBe("Vui lòng chọn dịch vụ.")

    fireEvent.change(screen.getByLabelText("Dịch vụ"), { target: { value: "2" } })
    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "18.000" } })
    fireEvent.change(screen.getByLabelText("Áp dụng từ tháng"), { target: { value: "2026-12" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))

    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith("token", 5, {
      serviceId: 2, price: 18000, unitCode: "m3", billingBasis: "usage", effectiveFrom: "2026-12-01", effectiveTo: null,
    }))
    expect(await screen.findByText(/Đã thêm giá Nước/)).toBeTruthy()
    await waitFor(() => expect(mocks.rates).toHaveBeenCalledTimes(2))
  })

  it("submits the backend default for Additional Occupant without a billing selector", async () => {
    mocks.create.mockResolvedValueOnce(
      rate({
        rateId: 4,
        serviceId: 8,
        serviceName: "Additional Occupant",
        price: 50000,
        unitCode: "person_month",
        billingBasis: "additional_occupant",
      }),
    )
    mocks.catalog.mockResolvedValueOnce([
      {
        serviceId: 8,
        name: "Additional Occupant",
        price: 0,
        defaultUnitCode: "person_month",
        defaultBillingBasis: "additional_occupant",
      },
    ])
    render(<PropertyServiceRatesPanel propertyId={5} />)
    await screen.findByRole("option", { name: "Additional Occupant" })

    fireEvent.change(screen.getByLabelText("Dịch vụ"), { target: { value: "8" } })
    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "50000" } })
    fireEvent.change(screen.getByLabelText("Áp dụng từ tháng"), { target: { value: "2026-12" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))

    await waitFor(() =>
      expect(mocks.create).toHaveBeenCalledWith("token", 5, {
        serviceId: 8,
        price: 50000,
        unitCode: "person_month",
        billingBasis: "additional_occupant",
        effectiveFrom: "2026-12-01",
        effectiveTo: null,
      }),
    )
    expect(await screen.findByText(/50\.000\s₫\/người\/tháng/)).toBeTruthy()
  })

  it("explains an overlapping period instead of a raw error", async () => {
    mocks.create.mockRejectedValue(new ApiError("overlap", { status: 409 }))
    render(<PropertyServiceRatesPanel propertyId={5} />)
    await screen.findByRole("option", { name: "Nước" })
    fireEvent.change(screen.getByLabelText("Dịch vụ"), { target: { value: "2" } })
    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "50000" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))
    expect((await screen.findByRole("alert")).textContent).toBe(
      "Kỳ áp dụng bị trùng với một mức giá đã có của dịch vụ này. Giá cũ không bị thay đổi.",
    )
  })

  it("blocks zero or decimal prices client-side and renders backend price ceiling errors under the price input", async () => {
    mocks.create.mockRejectedValueOnce(
      new ApiError("Giá điện vượt trần", {
        status: 400,
        validationErrors: {
          price: [
            "Giá điện tối đa 4.152đ/kWh (bậc cao nhất của EVN đã gồm VAT). Chủ trọ thu cao hơn hóa đơn điện có thể bị phạt.",
          ],
        },
      }),
    )
    render(<PropertyServiceRatesPanel propertyId={5} />)
    await screen.findByRole("option", { name: "Nước" })

    fireEvent.change(screen.getByLabelText("Dịch vụ"), { target: { value: "2" } })
    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "0" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))
    expect(screen.getByRole("alert").textContent).toBe("Đơn giá phải lớn hơn 0.")
    expect(mocks.create).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "10.5" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))
    expect(screen.getByRole("alert").textContent).toBe("Đơn giá phải là số đồng nguyên.")
    expect(mocks.create).not.toHaveBeenCalled()

    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "5000" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))

    expect((await screen.findByRole("alert")).textContent).toBe(
      "Giá điện tối đa 4.152đ/kWh (bậc cao nhất của EVN đã gồm VAT). Chủ trọ thu cao hơn hóa đơn điện có thể bị phạt.",
    )
    expect(screen.getByLabelText("Đơn giá (VND)").getAttribute("aria-invalid")).toBe("true")
  })
})
