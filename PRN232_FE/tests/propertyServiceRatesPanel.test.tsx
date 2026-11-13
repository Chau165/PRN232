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
  mocks.catalog.mockReset().mockResolvedValue([{ serviceId: 2, name: "Nước", price: 0 }])
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
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))
    expect(screen.getByRole("alert").textContent).toBe("Vui lòng chọn dịch vụ.")

    fireEvent.change(screen.getByLabelText("Dịch vụ"), { target: { value: "2" } })
    fireEvent.change(screen.getByLabelText("Cách tính tiền"), { target: { value: "m3" } })
    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "18.000" } })
    fireEvent.change(screen.getByLabelText("Áp dụng từ tháng"), { target: { value: "2026-12" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))

    await waitFor(() => expect(mocks.create).toHaveBeenCalledWith("token", 5, {
      serviceId: 2, price: 18000, unitCode: "m3", billingBasis: "usage", effectiveFrom: "2026-12-01", effectiveTo: null,
    }))
    expect(await screen.findByText(/Đã thêm giá Nước/)).toBeTruthy()
    await waitFor(() => expect(mocks.rates).toHaveBeenCalledTimes(2))
  })

  it("only offers billing methods that invoices can calculate", async () => {
    render(<PropertyServiceRatesPanel propertyId={5} />)
    const select = await screen.findByLabelText("Cách tính tiền")
    const labels = Array.from((select as HTMLSelectElement).options).map((option) => option.textContent)
    expect(labels).toEqual(["Chọn cách tính", "Theo số điện (kWh)", "Theo số nước (m³)", "Cố định mỗi phòng/tháng"])
  })

  it("explains an overlapping period instead of a raw error", async () => {
    mocks.create.mockRejectedValue(new ApiError("overlap", { status: 409 }))
    render(<PropertyServiceRatesPanel propertyId={5} />)
    await screen.findByRole("option", { name: "Nước" })
    fireEvent.change(screen.getByLabelText("Dịch vụ"), { target: { value: "2" } })
    fireEvent.change(screen.getByLabelText("Cách tính tiền"), { target: { value: "room" } })
    fireEvent.change(screen.getByLabelText("Đơn giá (VND)"), { target: { value: "50000" } })
    fireEvent.click(screen.getByRole("button", { name: "Thêm mức giá" }))
    expect((await screen.findByRole("alert")).textContent).toBe(
      "Kỳ áp dụng bị trùng với một mức giá đã có của dịch vụ này. Giá cũ không bị thay đổi.",
    )
  })
})
