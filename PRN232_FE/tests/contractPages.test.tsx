import { fireEvent, render, screen, waitFor, within } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import CreateContractPage from "@/pages/CreateContractPage/CreateContractPage"
import ManagementContractsPage from "@/pages/ManagementContractsPage/ManagementContractsPage"
import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"
import type { ContractBillingTimelineDto, ContractDto } from "@/types/contract"
import type { InvoiceDto } from "@/types/invoice"
import { addContractMonths, formatContractDate, localToday } from "@/utils/managementContracts"

const mocks = vi.hoisted(() => ({
  getBooking: vi.fn(),
  getContracts: vi.fn(),
  createContract: vi.fn(),
  getContractBillingTimeline: vi.fn(),
  extendContract: vi.fn(),
  terminateContract: vi.fn(),
  markTerminatedRoomReady: vi.fn(),
  role: "MANAGER" as string,
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "manager-token", user: { id: "7", role: mocks.role } }),
}))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/services/bookingService", () => ({ getBooking: mocks.getBooking }))
vi.mock("@/services/contractService", () => ({
  getContracts: mocks.getContracts,
  createContract: mocks.createContract,
  getContractBillingTimeline: mocks.getContractBillingTimeline,
  extendContract: mocks.extendContract,
  terminateContract: mocks.terminateContract,
  markTerminatedRoomReady: mocks.markTerminatedRoomReady,
}))

const booking = (overrides: Partial<BookingDto> = {}): BookingDto => ({
  bookingId: 9, roomId: 1, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", depositAmount: 3_000_000, status: "Deposited",
  expiryDate: new Date(Date.now() + 86_400_000).toISOString(), customerPhone: "0908123456", contractId: null,
  ...overrides,
})

const contract = (overrides: Partial<ContractDto> = {}): ContractDto => ({
  contractId: 12, bookingId: 9, roomId: 1, roomCode: "A101", propertyId: 1, propertyName: "Sunrise",
  customerId: 10, customerName: "Nguyễn An", customerPhone: "0908123456",
  startDate: "2026-11-01", endDate: "2027-11-01", monthlyRent: 3_000_000, depositAmount: 3_000_000,
  status: "Active", processedByStaffName: "Manager A",
  prepaidRentAmount: 3_000_000, prepaidRentApplied: 0, remainingPrepaidCredit: 3_000_000,
  propertyAddress: "12 Sunrise St", lessorUserId: 2, lessorName: "Owner A", lessorAddress: "12 Sunrise St",
  displayStatus: "Active",
  ...overrides,
})

const unpaidInvoice: InvoiceDto = {
  invoiceId: 81,
  contractId: 12,
  roomCode: "A101",
  propertyName: "Sunrise",
  customerName: "Nguyễn An",
  invoiceMonth: "2026-10-01",
  dueDate: "2026-10-15",
  status: "Overdue",
  total: 3_237_500,
  lines: [],
  prepaidRentApplied: 0,
  netPayable: 3_237_500,
  amountPaid: 0,
  remainingPayable: 3_237_500,
  displayStatus: "Overdue",
}

function billingTimeline(invoices: InvoiceDto[] = []): ContractBillingTimelineDto {
  return { contract: contract(), issuedInvoices: invoices, plannedBillingPeriods: [], hasMorePlannedPeriods: false }
}

function renderCreatePage() {
  return render(
    <MemoryRouter initialEntries={["/management/contracts/new?bookingId=9"]}>
      <Routes>
        <Route path="/management/contracts/new" element={<CreateContractPage />} />
        <Route path="/management/contracts" element={<p>contract list page</p>} />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  mocks.role = "MANAGER"
  mocks.getBooking.mockResolvedValue(booking())
  mocks.createContract.mockResolvedValue(contract())
  mocks.getContracts.mockResolvedValue([contract()])
  mocks.getContractBillingTimeline.mockResolvedValue(billingTimeline())
  mocks.extendContract.mockResolvedValue(contract())
  mocks.terminateContract.mockResolvedValue({ contract: contract({ status: "Terminated", displayStatus: "Terminated" }), outstandingDebt: 0 })
  mocks.markTerminatedRoomReady.mockResolvedValue(undefined)
})

describe("CreateContractPage", () => {
  it("creates a contract from a deposited booking with a quick term, then opens the contract list", async () => {
    renderCreatePage()
    await screen.findByText("Booking #9 · Đã đặt cọc")
    expect(mocks.getBooking).toHaveBeenCalledWith("manager-token", 9, expect.any(AbortSignal))
    expect((screen.getByLabelText("Số điện thoại người thuê") as HTMLInputElement).value).toBe("0908123456")

    fireEvent.click(screen.getByRole("button", { name: "1 năm" }))
    expect(screen.getByRole("button", { name: "1 năm" }).getAttribute("aria-pressed")).toBe("true")
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận hợp đồng" }))

    await screen.findByText("contract list page")
    const today = localToday()
    expect(mocks.createContract).toHaveBeenCalledWith("manager-token", {
      bookingId: 9, startDate: today, endDate: addContractMonths(today, 12),
    })
  })

  it.each([
    [booking({ status: "Pending" }), "Chỉ booking đã đặt cọc mới lập được hợp đồng."],
    [booking({ contractId: 12 }), "Booking này đã được lập hợp đồng."],
  ])("explains why a booking cannot become a contract", async (current, message) => {
    mocks.getBooking.mockResolvedValue(current)
    renderCreatePage()

    expect((await screen.findByRole("alert")).textContent).toBe(message)
    expect(screen.queryByRole("button", { name: "Xác nhận hợp đồng" })).toBeNull()
  })

  it("explains a conflict reported by the server", async () => {
    mocks.createContract.mockRejectedValue(new ApiError("Room already has an active contract.", { status: 409 }))
    renderCreatePage()
    fireEvent.click(await screen.findByRole("button", { name: "6 tháng" }))
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận hợp đồng" }))

    expect((await screen.findByRole("alert")).textContent)
      .toBe("Booking vừa thay đổi hoặc phòng đã có hợp đồng khác. Vui lòng tải lại trang.")
    expect(screen.queryByText("contract list page")).toBeNull()
  })
})

describe("ManagementContractsPage", () => {
  it("lists real contracts, confirms the one just created and shows its details", async () => {
    render(
      <MemoryRouter initialEntries={[{ pathname: "/management/contracts", state: { createdContractId: 12 } }]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    expect(screen.getByText("Đã tạo hợp đồng #12 thành công.")).toBeTruthy()
    expect(screen.getByText("Đang hiệu lực")).toBeTruthy()
    expect(screen.getByText("01/11/2026")).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Xem" }))
    expect(await screen.findByText("Manager A")).toBeTruthy()
    expect(screen.getByText("0908123456")).toBeTruthy()
    expect(screen.getByText("#9")).toBeTruthy()
  })

  it("loads billing data and calls the real extend endpoint before refreshing the list", async () => {
    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )
    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Gia hạn hợp đồng" }))
    const dialog = await screen.findByRole("dialog")
    await screen.findByText("Không có công nợ từ các hóa đơn đã phát hành.")
    fireEvent.click(within(dialog).getByRole("button", { name: "Thêm 6 tháng" }))
    const newEndDate = addContractMonths(contract().endDate, 6)
    expect((within(dialog).getByLabelText("Ngày kết thúc mới") as HTMLInputElement).value).toBe(newEndDate)
    fireEvent.click(within(dialog).getByRole("button", { name: "Xác nhận gia hạn" }))

    expect(await screen.findByText(`Đã gia hạn hợp đồng #12 đến ${formatContractDate(newEndDate)}.`)).toBeTruthy()
    expect(mocks.getContractBillingTimeline).toHaveBeenCalledWith("manager-token", 12, expect.any(AbortSignal))
    expect(mocks.extendContract).toHaveBeenCalledWith("manager-token", 12, { newEndDate })
    await waitFor(() => expect(mocks.getContracts).toHaveBeenCalledTimes(2))
  })

  it("shows outstanding debt and requires checkout and debt acknowledgements before termination", async () => {
    mocks.getContractBillingTimeline.mockResolvedValue(billingTimeline([unpaidInvoice]))
    mocks.terminateContract.mockResolvedValue({
      contract: contract({ status: "Terminated", displayStatus: "Terminated" }),
      outstandingDebt: 3_237_500,
    })
    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )
    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Xác nhận kết thúc / Trả phòng" }))
    const dialog = await screen.findByRole("dialog")
    expect(await within(dialog).findByText(/Khách thuê còn công nợ 3\.237\.500/)).toBeTruthy()
    expect(within(dialog).getByText(/vẫn có trách nhiệm thanh toán sau khi hợp đồng kết thúc/)).toBeTruthy()
    expect(within(dialog).getByRole("button", { name: "Xác nhận kết thúc / Trả phòng" }).hasAttribute("disabled")).toBe(true)

    fireEvent.click(within(dialog).getByLabelText(/Manager xác nhận Customer đã thực tế bàn giao phòng/))
    expect(within(dialog).getByRole("button", { name: "Xác nhận kết thúc / Trả phòng" }).hasAttribute("disabled")).toBe(true)
    fireEvent.click(within(dialog).getByLabelText(/Tôi đã đọc cảnh báo công nợ/))
    fireEvent.click(within(dialog).getByRole("button", { name: "Xác nhận kết thúc / Trả phòng" }))

    expect(await screen.findByText(/Công nợ 3\.237\.500\s*₫ vẫn được giữ nguyên/)).toBeTruthy()
    expect(mocks.terminateContract).toHaveBeenCalledWith("manager-token", 12, {
      checkoutConfirmed: true,
      roomReadyForRental: false,
      outstandingDebtAcknowledged: true,
    })
  })

  it("does not expose checkout operations to Admin", async () => {
    mocks.role = "ADMIN"
    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    expect(screen.queryByRole("button", { name: "Gia hạn hợp đồng" })).toBeNull()
    expect(screen.queryByRole("button", { name: "Xác nhận kết thúc / Trả phòng" })).toBeNull()
  })

  it("lets an assigned Manager mark a terminated room ready through the API", async () => {
    mocks.getContracts.mockResolvedValue([
      contract({ status: "Terminated", displayStatus: "Terminated" }),
    ])
    vi.spyOn(window, "confirm").mockReturnValue(true)
    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Đánh dấu phòng sẵn sàng" }))

    expect(await screen.findByText("Phòng A101 đã được đánh dấu sẵn sàng cho thuê.")).toBeTruthy()
    expect(mocks.markTerminatedRoomReady).toHaveBeenCalledWith("manager-token", 12)
  })
})
