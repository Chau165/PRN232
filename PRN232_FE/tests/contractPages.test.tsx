import { fireEvent, render, screen } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import CreateContractPage from "@/pages/CreateContractPage/CreateContractPage"
import ManagementContractsPage from "@/pages/ManagementContractsPage/ManagementContractsPage"
import { ApiError } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"
import type { ContractDto } from "@/types/contract"
import { addContractMonths, localToday } from "@/utils/managementContracts"

const mocks = vi.hoisted(() => ({
  getBooking: vi.fn(),
  getContracts: vi.fn(),
  createContract: vi.fn(),
}))

vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({ accessToken: "manager-token", user: { id: "7", role: "MANAGER" } }),
}))
vi.mock("@/components/management/ManagementPage", () => ({
  default: ({ children }: { children: React.ReactNode }) => <main>{children}</main>,
}))
vi.mock("@/services/bookingService", () => ({ getBooking: mocks.getBooking }))
vi.mock("@/services/contractService", () => ({ getContracts: mocks.getContracts, createContract: mocks.createContract }))

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
  ...overrides,
})

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
  mocks.getBooking.mockResolvedValue(booking())
  mocks.createContract.mockResolvedValue(contract())
  mocks.getContracts.mockResolvedValue([contract()])
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
})
