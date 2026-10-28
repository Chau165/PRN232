import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import CreateInvoiceModal from "@/components/management/CreateInvoiceModal"
import ManagementInvoicesPage from "@/pages/ManagementInvoicesPage/ManagementInvoicesPage"
import MyBillsPage from "@/pages/MyBillsPage/MyBillsPage"
import type { ContractDto } from "@/api/contractsApi"
import type { InvoiceDto } from "@/api/invoicesApi"
import type { UtilityReadingDto } from "@/api/utilityReadingsApi"
import { defaultDueDate } from "@/utils/invoiceDisplay"
import { localToday } from "@/utils/managementContracts"

const mocks = vi.hoisted(() => ({
  getContracts: vi.fn(),
  getInvoices: vi.fn(),
  previewInvoice: vi.fn(),
  createInvoice: vi.fn(),
  getUtilityReadings: vi.fn(),
  createUtilityReading: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "token", user: { id: "7", role: "MANAGER" } }),
  useOptionalAuth: () => null,
}))
vi.mock("@/auth/CustomerNotificationsContext", () => ({
  useCustomerNotificationsState: () => ({
    notifications: [], unreadCount: 0, loading: false, error: null, refresh: vi.fn(), markRead: vi.fn(),
  }),
}))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/utils/managementAuth", () => ({ getCurrentUser: () => ({ id: "7", name: "Manager", role: "MANAGER" }) }))
vi.mock("@/utils/managementScope", () => ({ getManagementScope: () => ({ propertyIds: [] }) }))
vi.mock("@/api/contractsApi", () => ({ getContracts: mocks.getContracts }))
vi.mock("@/api/invoicesApi", () => ({
  getInvoices: mocks.getInvoices, previewInvoice: mocks.previewInvoice, createInvoice: mocks.createInvoice,
}))
vi.mock("@/api/utilityReadingsApi", () => ({
  getUtilityReadings: mocks.getUtilityReadings, createUtilityReading: mocks.createUtilityReading,
}))

const today = localToday()
const thisMonth = `${today.slice(0, 7)}-01`

const contract = (overrides: Partial<ContractDto> = {}): ContractDto => ({
  contractId: 1, bookingId: 9, roomId: 1, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", customerPhone: "0908123456", startDate: "2026-06-01", endDate: "2099-05-31",
  monthlyRent: 3_000_000, depositAmount: 3_000_000, status: "Active", processedByStaffName: "Manager A",
  ...overrides,
})

const reading = (overrides: Partial<UtilityReadingDto> = {}): UtilityReadingDto => ({
  readingId: 1, contractId: 1, roomCode: "A101", propertyName: "Sunrise", customerName: "Nguyễn An",
  readingMonth: thisMonth, electricStart: 100, electricEnd: 150, electricUsage: 50, waterStart: 10, waterEnd: 14, waterUsage: 4,
  ...overrides,
})

const invoice = (overrides: Partial<InvoiceDto> = {}): InvoiceDto => ({
  invoiceId: 5, contractId: 1, roomCode: "A101", propertyName: "Sunrise", customerName: "Nguyễn An",
  invoiceMonth: thisMonth, dueDate: "2099-01-10", status: "Unpaid", total: 3_355_000,
  lines: [
    { itemType: "Rent", description: "Tiền phòng", quantity: null, unitPrice: null, unitCode: null, amount: 3_000_000 },
    { itemType: "Electricity", description: "Electricity", quantity: 50, unitPrice: 3_500, unitCode: "kwh", amount: 175_000 },
    { itemType: "Water", description: "Water", quantity: 4, unitPrice: 20_000, unitCode: "m3", amount: 80_000 },
    { itemType: "Service", description: "Internet - monthly", quantity: 1, unitPrice: 100_000, unitCode: "room_month", amount: 100_000 },
  ],
  ...overrides,
})

beforeEach(() => {
  mocks.getContracts.mockResolvedValue([contract(), contract({ contractId: 2, roomCode: "B202", status: "Terminated" })])
  mocks.getUtilityReadings.mockResolvedValue([])
  mocks.createUtilityReading.mockResolvedValue(reading())
  mocks.previewInvoice.mockResolvedValue(invoice({ invoiceId: 0, status: "Preview" }))
  mocks.createInvoice.mockResolvedValue(invoice())
  mocks.getInvoices.mockResolvedValue([])
})

describe("CreateInvoiceModal", () => {
  it("records the first reading, previews the server total, then creates the invoice", async () => {
    const onCreated = vi.fn()
    render(<CreateInvoiceModal onClose={vi.fn()} onCreated={onCreated} />)

    await screen.findByRole("option", { name: /Phòng A101/ })
    expect(screen.queryByRole("option", { name: /Phòng B202/ })).toBeNull()
    fireEvent.change(screen.getByLabelText("Hợp đồng / phòng"), { target: { value: "1" } })
    await screen.findByText("Đây là lần ghi đầu tiên của hợp đồng, cần nhập cả chỉ số đầu kỳ.")

    fireEvent.change(screen.getByLabelText("Chỉ số điện đầu kỳ"), { target: { value: "100" } })
    fireEvent.change(screen.getByLabelText("Chỉ số nước đầu kỳ"), { target: { value: "10" } })
    fireEvent.change(screen.getByLabelText("Chỉ số điện cuối kỳ"), { target: { value: "150" } })
    fireEvent.change(screen.getByLabelText("Chỉ số nước cuối kỳ"), { target: { value: "14" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số" }))
    await screen.findByText(/Đã ghi: điện 100 → 150 \(50 kWh\)/)
    expect(mocks.createUtilityReading).toHaveBeenCalledWith("token", {
      contractId: 1, readingMonth: thisMonth, electricEnd: 150, waterEnd: 14, electricStart: 100, waterStart: 10,
    })

    fireEvent.click(screen.getByRole("button", { name: "Xem trước hóa đơn" }))
    await screen.findByRole("button", { name: "Tạo hóa đơn" })
    const request = { contractId: 1, invoiceMonth: thisMonth, dueDate: defaultDueDate(thisMonth, today) }
    expect(mocks.previewInvoice).toHaveBeenCalledWith("token", request)
    expect(screen.getByText(/^Điện \(50 kWh/)).toBeTruthy()
    expect(screen.getByText(/3\.355\.000/)).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Tạo hóa đơn" }))
    await vi.waitFor(() => expect(onCreated).toHaveBeenCalledWith(invoice()))
    expect(mocks.createInvoice).toHaveBeenCalledWith("token", request)
  })

  it("continues from the previous reading and blocks lower meter values before calling the API", async () => {
    mocks.getUtilityReadings.mockResolvedValue([reading({ readingMonth: "2000-01-01", electricEnd: 1320, waterEnd: 104 })])
    render(<CreateInvoiceModal onClose={vi.fn()} onCreated={vi.fn()} />)
    await screen.findByRole("option", { name: /Phòng A101/ })
    fireEvent.change(screen.getByLabelText("Hợp đồng / phòng"), { target: { value: "1" } })

    await screen.findByText(/Chỉ số đầu kỳ lấy từ tháng 01\/2000: điện 1320, nước 104/)
    expect(screen.queryByLabelText("Chỉ số điện đầu kỳ")).toBeNull()
    fireEvent.change(screen.getByLabelText("Chỉ số điện cuối kỳ"), { target: { value: "1300" } })
    fireEvent.change(screen.getByLabelText("Chỉ số nước cuối kỳ"), { target: { value: "110" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu chỉ số" }))

    expect(screen.getByRole("alert").textContent).toBe("Chỉ số tháng này không được nhỏ hơn chỉ số đầu kỳ.")
    expect(mocks.createUtilityReading).not.toHaveBeenCalled()
  })
})

describe("ManagementInvoicesPage", () => {
  it("lists real invoices, marks overdue ones and shows the priced lines", async () => {
    mocks.getInvoices.mockResolvedValue([invoice({ dueDate: "2000-01-10" })])
    render(<MemoryRouter><ManagementInvoicesPage /></MemoryRouter>)

    await screen.findByText("HĐ #5")
    expect(screen.getByText("Quá hạn")).toBeTruthy()
    expect(screen.queryByRole("button", { name: "Đã thu" })).toBeNull()

    fireEvent.click(screen.getByRole("button", { name: "Xem" }))
    expect(await screen.findByText("Tiền phòng")).toBeTruthy()
    expect(screen.getByText(/^Nước \(4 m³/)).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "+ Tạo hóa đơn" }))
    expect(screen.getByRole("dialog", { name: "Tạo hóa đơn tháng" })).toBeTruthy()
  })
})

describe("MyBillsPage", () => {
  it("separates unpaid and paid invoices of the tenant", async () => {
    mocks.getInvoices.mockResolvedValue([invoice(), invoice({ invoiceId: 4, status: "Paid", invoiceMonth: "2026-09-01" })])
    render(<MemoryRouter><MyBillsPage /></MemoryRouter>)

    await screen.findByText("HĐ #5")
    expect(screen.queryByText("HĐ #4")).toBeNull()

    fireEvent.click(screen.getByRole("button", { name: "Đã thanh toán" }))
    expect(screen.getByText("HĐ #4")).toBeTruthy()
    expect(screen.queryByText("HĐ #5")).toBeNull()

    fireEvent.click(screen.getByRole("button", { name: "Xem chi tiết" }))
    expect(await screen.findByText("Hóa đơn tháng 09/2026")).toBeTruthy()
  })
})
