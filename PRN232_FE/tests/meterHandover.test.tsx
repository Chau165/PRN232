import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import MeterHandoverModal from "@/components/management/MeterHandoverModal"
import ContractMeterHandover from "@/components/customer/ContractMeterHandover"
import ManagementContractsPage from "@/pages/ManagementContractsPage/ManagementContractsPage"
import CreateInvoiceModal from "@/components/management/CreateInvoiceModal"
import { ApiError } from "@/api/apiClient"
import type { ContractDto } from "@/types/contract"
import { localToday } from "@/utils/managementContracts"

const mocks = vi.hoisted(() => ({
  getContracts: vi.fn(), getContract: vi.fn(), saveContractMeterHandover: vi.fn(),
  getUtilityReadings: vi.fn(), createUtilityReading: vi.fn(), previewInvoice: vi.fn(), createInvoice: vi.fn(),
  role: "MANAGER",
}))
vi.mock("@/auth/AuthContext", () => ({ useAuth: () => ({ accessToken: "token", user: { role: mocks.role } }) }))
vi.mock("@/services/contractService", () => ({
  getContracts: mocks.getContracts, getContract: mocks.getContract, saveContractMeterHandover: mocks.saveContractMeterHandover,
  getContractBillingTimeline: vi.fn(), extendContract: vi.fn(), terminateContract: vi.fn(), markTerminatedRoomReady: vi.fn(),
}))
vi.mock("@/services/utilityReadingService", () => ({ getUtilityReadings: mocks.getUtilityReadings, createUtilityReading: mocks.createUtilityReading }))
vi.mock("@/services/invoiceService", () => ({ previewInvoice: mocks.previewInvoice, createInvoice: mocks.createInvoice }))
vi.mock("@/components/management/ManagementPage", () => ({ default: ({ children }: { children: React.ReactNode }) => <main>{children}</main> }))

const today = localToday()
const contract = (overrides: Partial<ContractDto> = {}): ContractDto => ({
  contractId: 12, bookingId: 9, roomId: 1, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", customerPhone: "0908123456", startDate: `${today.slice(0, 7)}-01`, endDate: "2099-12-31",
  monthlyRent: 3_000_000, depositAmount: 3_000_000, status: "Active", processedByStaffName: "Manager",
  prepaidRentAmount: 3_000_000, prepaidRentApplied: 0, remainingPrepaidCredit: 3_000_000,
  propertyAddress: null, lessorUserId: 2, lessorName: "Owner", lessorAddress: null, displayStatus: "Active",
  initialElectricReading: null, initialWaterReading: null, meterHandoverDate: null, hasUtilityReadings: false, hasInvoices: false,
  ...overrides,
})
const recorded = () => contract({ initialElectricReading: 1200, initialWaterReading: 45, meterHandoverDate: today })

beforeEach(() => {
  vi.resetAllMocks()
  mocks.role = "MANAGER"
  mocks.getContracts.mockResolvedValue([contract()])
  mocks.getContract.mockResolvedValue(recorded())
  mocks.saveContractMeterHandover.mockResolvedValue(recorded())
  mocks.getUtilityReadings.mockResolvedValue([])
})

function fill() {
  fireEvent.change(screen.getByLabelText("Chỉ số điện ban đầu (kWh)"), { target: { value: "1200" } })
  fireEvent.change(screen.getByLabelText("Chỉ số nước ban đầu (m³)"), { target: { value: "45" } })
}

describe("contract meter handover", () => {
  it("saves the selected contract, waits for the API, and refetches before reporting success", async () => {
    const onSaved = vi.fn()
    render(<MeterHandoverModal contract={contract()} onClose={vi.fn()} onSaved={onSaved} />)
    expect(screen.getByText(/Sunrise · Phòng A101 · Nguyễn An/)).toBeTruthy()
    expect((screen.getByLabelText("Ngày bàn giao") as HTMLInputElement).max).toBe(today)
    expect(screen.queryByLabelText("Chỉ số điện cuối kỳ")).toBeNull()
    fill()
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số bàn giao" }))
    await waitFor(() => expect(onSaved).toHaveBeenCalledWith(recorded()))
    expect(mocks.saveContractMeterHandover).toHaveBeenCalledWith("token", 12, {
      meterHandoverDate: today, initialElectricReading: 1200, initialWaterReading: 45, expectedMeterHandover: null,
    })
    expect(mocks.getContract).toHaveBeenCalledWith("token", 12)
    expect(mocks.createUtilityReading).not.toHaveBeenCalled()
    expect(mocks.createInvoice).not.toHaveBeenCalled()
  })

  it.each([403, 409, 500])("shows %s without a success callback or fake UI update", async (status) => {
    mocks.saveContractMeterHandover.mockRejectedValue(new ApiError("Save failed", { status }))
    const onSaved = vi.fn()
    render(<MeterHandoverModal contract={contract()} onClose={vi.fn()} onSaved={onSaved} />)
    fill()
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số bàn giao" }))
    await screen.findByRole("alert")
    expect(onSaved).not.toHaveBeenCalled()
    expect(mocks.getContract).not.toHaveBeenCalled()
  })

  it("requires confirmation when editing and sends the observed values", async () => {
    const confirm = vi.spyOn(window, "confirm").mockReturnValueOnce(false).mockReturnValueOnce(true)
    render(<MeterHandoverModal contract={recorded()} onClose={vi.fn()} onSaved={vi.fn()} />)
    fireEvent.change(screen.getByLabelText("Chỉ số điện ban đầu (kWh)"), { target: { value: "1201" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số bàn giao" }))
    expect(mocks.saveContractMeterHandover).not.toHaveBeenCalled()
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số bàn giao" }))
    await waitFor(() => expect(mocks.saveContractMeterHandover).toHaveBeenCalledWith("token", 12, {
      meterHandoverDate: today, initialElectricReading: 1201, initialWaterReading: 45,
      expectedMeterHandover: { meterHandoverDate: today, initialElectricReading: 1200, initialWaterReading: 45 },
    }))
    confirm.mockRestore()
  })

  it("rejects invalid inputs before the API and displays a genuine zero baseline", () => {
    const { unmount } = render(<MeterHandoverModal contract={contract()} onClose={vi.fn()} onSaved={vi.fn()} />)
    fill()
    fireEvent.change(screen.getByLabelText("Chỉ số điện ban đầu (kWh)"), { target: { value: "-1" } })
    fireEvent.submit(screen.getByRole("dialog"))
    expect(mocks.saveContractMeterHandover).not.toHaveBeenCalled()
    expect(screen.getByRole("alert")).toBeTruthy()
    unmount()
    render(<ContractMeterHandover contract={contract({ meterHandoverDate: today, initialElectricReading: 0, initialWaterReading: 0 })} />)
    expect(screen.getByText("0 kWh")).toBeTruthy()
    expect(screen.getByText("0 m³")).toBeTruthy()
    expect(screen.queryByRole("textbox")).toBeNull()
  })

  it("shows the manager action and updates the selected detail after saving", async () => {
    render(<MemoryRouter><ManagementContractsPage /></MemoryRouter>)
    fireEvent.click(await screen.findByRole("button", { name: "Xem" }))
    expect(screen.getByText("Chưa ghi nhận chỉ số bàn giao.")).toBeTruthy()
    fireEvent.click(screen.getByRole("button", { name: "Ghi nhận chỉ số bàn giao" }))
    fill()
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số bàn giao" }))
    await screen.findByText("1.200 kWh")
    await screen.findByText("Đã lưu chỉ số bàn giao thành công.")
  })

  it.each(["ADMIN", "CUSTOMER"])("hides manager writes for %s", async (role) => {
    mocks.role = role
    render(<MemoryRouter><ManagementContractsPage /></MemoryRouter>)
    await screen.findByRole("button", { name: "Xem" })
    expect(screen.queryByRole("button", { name: "Ghi nhận chỉ số bàn giao" })).toBeNull()
  })

  it("keeps a legacy contract read only and displays its recorded history", async () => {
    mocks.getContracts.mockResolvedValue([contract({ hasUtilityReadings: true, hasInvoices: true })])
    mocks.getUtilityReadings.mockResolvedValue([{ readingId: 1, readingMonth: `${today.slice(0, 7)}-01`, electricStart: 9979, electricEnd: 10000, waterStart: 123, waterEnd: 155 }])
    render(<MemoryRouter><ManagementContractsPage /></MemoryRouter>)
    fireEvent.click(await screen.findByRole("button", { name: "Xem" }))
    expect(screen.queryByRole("button", { name: /chỉ số bàn giao/ })).toBeNull()
    await screen.findByText(/điện 9979 → 10000 kWh; nước 123 → 155 m³/)
    expect(screen.getByText(/Hợp đồng lịch sử chưa có thông tin bàn giao/)).toBeTruthy()
  })

  it("guides a missing baseline to the shared form without offering editable starting readings", async () => {
    render(<CreateInvoiceModal onClose={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByRole("option", { name: /Phòng A101/ })
    fireEvent.change(screen.getByLabelText("Hợp đồng / phòng"), { target: { value: "12" } })
    await screen.findByText(/Chưa có chỉ số điện nước bàn giao/)
    expect(screen.queryByLabelText("Chỉ số điện đầu kỳ")).toBeNull()
    expect(screen.queryByLabelText("Chỉ số điện cuối kỳ")).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Ghi nhận chỉ số bàn giao" }))
    fill()
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số bàn giao" }))
    await screen.findByText("Chỉ số đầu kỳ được lấy từ dữ liệu bàn giao phòng.")
    expect((screen.getByLabelText("Chỉ số điện đầu kỳ") as HTMLInputElement).readOnly).toBe(true)
    expect(mocks.createInvoice).not.toHaveBeenCalled()
  })

  it("fails closed while readings load, shows the load failure, and retries", async () => {
    mocks.getUtilityReadings.mockRejectedValueOnce(new Error("Reading load failed")).mockResolvedValueOnce([])
    render(<CreateInvoiceModal onClose={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByRole("option", { name: /Phòng A101/ })
    fireEvent.change(screen.getByLabelText("Hợp đồng / phòng"), { target: { value: "12" } })
    await screen.findByText("Reading load failed")
    expect(screen.queryByRole("button", { name: "Lưu chỉ số cuối kỳ" })).toBeNull()
    fireEvent.click(screen.getByRole("button", { name: "Thử lại" }))
    await screen.findByText(/Chưa có chỉ số điện nước bàn giao/)
    expect(mocks.getUtilityReadings).toHaveBeenCalledTimes(2)
  })

  it("supports date entry, traps keyboard focus, dismisses, and restores the opener's focus", () => {
    const opener = document.createElement("button")
    document.body.appendChild(opener)
    opener.focus()
    const onClose = vi.fn()
    const { unmount } = render(<MeterHandoverModal contract={contract()} onClose={onClose} onSaved={vi.fn()} />)
    const date = screen.getByLabelText("Ngày bàn giao")
    expect(document.activeElement).toBe(date)
    fireEvent.change(date, { target: { value: contract().startDate } })
    expect((date as HTMLInputElement).value).toBe(contract().startDate)
    fireEvent.keyDown(date, { key: "Tab", shiftKey: true })
    expect(document.activeElement).toBe(screen.getByRole("button", { name: "Lưu chỉ số bàn giao" }))
    fireEvent.keyDown(document.activeElement!, { key: "Tab" })
    expect(document.activeElement).toBe(date)
    fireEvent.keyDown(date, { key: "Escape" })
    expect(onClose).toHaveBeenCalledTimes(1)
    fireEvent.click(screen.getByRole("button", { name: "Đóng" }))
    expect(onClose).toHaveBeenCalledTimes(2)
    unmount()
    expect(document.activeElement).toBe(opener)
    opener.remove()
  })

  it("retries historical readings without creating a baseline or enabling writes", async () => {
    mocks.getUtilityReadings.mockRejectedValueOnce(new Error("Network failed")).mockResolvedValueOnce([
      { readingId: 1, readingMonth: `${today.slice(0, 7)}-01`, electricStart: 9979, electricEnd: 10000, waterStart: 123, waterEnd: 155 },
    ])
    render(<ContractMeterHandover contract={contract({ hasUtilityReadings: true })} />)
    await screen.findByText("Không thể tải lịch sử chỉ số.")
    fireEvent.click(screen.getByRole("button", { name: "Thử lại" }))
    await screen.findByText(/điện 9979 → 10000 kWh/)
    expect(mocks.saveContractMeterHandover).not.toHaveBeenCalled()
  })
})
