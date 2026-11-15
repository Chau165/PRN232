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
import { getVietnamTodayIsoDate } from "@/utils/equipmentValidation"
import { addIsoDays } from "@/utils/moveOutNoticeValidation"

const mocks = vi.hoisted(() => ({
  getBooking: vi.fn(),
  getContracts: vi.fn(),
  createContract: vi.fn(),
  getContractBillingTimeline: vi.fn(),
  extendContract: vi.fn(),
  terminateContract: vi.fn(),
  markTerminatedRoomReady: vi.fn(),
  getContractHandovers: vi.fn(),
  saveContractMoveInHandover: vi.fn(),
  saveContractMoveOutHandover: vi.fn(),
  getContractEvidence: vi.fn(),
  uploadContractEvidence: vi.fn(),
  deleteContractEvidence: vi.fn(),
  hideContractEvidence: vi.fn(),
  saveContractMoveOutNotice: vi.fn(),
  cancelContractMoveOutNotice: vi.fn(),
  getContractDepositSettlement: vi.fn(),
  confirmContractDepositSettlement: vi.fn(),
  confirmDepositSettlementRefund: vi.fn(),
  confirmDepositSettlementDueCollected: vi.fn(),
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
vi.mock("@/services/contractHandoverService", () => ({
  getContractHandovers: mocks.getContractHandovers,
  saveContractMoveInHandover: mocks.saveContractMoveInHandover,
  saveContractMoveOutHandover: mocks.saveContractMoveOutHandover,
}))
vi.mock("@/services/contractEvidenceService", () => ({
  getContractEvidence: mocks.getContractEvidence,
  uploadContractEvidence: mocks.uploadContractEvidence,
  deleteContractEvidence: mocks.deleteContractEvidence,
  hideContractEvidence: mocks.hideContractEvidence,
}))
vi.mock("@/services/contractMoveOutNoticeService", () => ({
  saveContractMoveOutNotice: mocks.saveContractMoveOutNotice,
  cancelContractMoveOutNotice: mocks.cancelContractMoveOutNotice,
}))
vi.mock("@/services/contractSettlementService", () => ({
  getContractDepositSettlement: mocks.getContractDepositSettlement,
  confirmContractDepositSettlement: mocks.confirmContractDepositSettlement,
  confirmDepositSettlementRefund: mocks.confirmDepositSettlementRefund,
  confirmDepositSettlementDueCollected:
    mocks.confirmDepositSettlementDueCollected,
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
  mocks.getContractHandovers.mockResolvedValue({
    contractId: 12,
    moveIn: null,
    moveOut: null,
    roomEquipments: [
      { equipmentId: 501, name: "Máy lạnh", quantity: 1, compensationValue: 8_500_000 },
      { equipmentId: 502, name: "Bàn học", quantity: 2, compensationValue: 500_000 },
    ],
  })
  mocks.getContractEvidence.mockResolvedValue({
    contractId: 12,
    moveInUploadDeadline: "2026-11-27",
    canUploadMoveIn: true,
    canUploadMoveOut: true,
    canUploadSignedContract: true,
    files: [],
  })
  mocks.getContractDepositSettlement.mockResolvedValue({
    contractId: 12,
    isSettled: false,
    moveOutDate: "2026-11-15",
    contractEndDate: "2027-11-01",
    depositAmount: 3_000_000,
    damageCharges: 1_200_000,
    damageCoveredByDeposit: 1_200_000,
    outstandingDebt: 0,
    retainedForDebt: 0,
    forfeitedAmount: 0,
    refundAmount: 1_800_000,
    amountDue: 0,
    earlyWithoutNotice: false,
    noticeDays: 40,
    note: null,
    settledByName: null,
    settledAtUtc: null,
    refundConfirmedAtUtc: null,
    refundConfirmedByName: null,
    dueCollectedAtUtc: null,
    dueCollectedByName: null,
  })
})

describe("CreateContractPage", () => {
  it("creates a contract from a deposited booking with a quick term, then opens the contract list", async () => {
    mocks.getBooking.mockResolvedValue(booking({ depositAppliesToFirstMonth: false }))
    renderCreatePage()
    await screen.findByText("Booking #9 · Đã đặt cọc")
    expect(mocks.getBooking).toHaveBeenCalledWith("manager-token", 9, expect.any(AbortSignal))
    expect(screen.getByText(/Tiền cọc được giữ làm tiền đảm bảo trong suốt thời gian thuê/)).toBeTruthy()
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
  it("lists real contracts, confirms the one just created and shows its details including securityDepositAmount (C13)", async () => {
    mocks.getContracts.mockResolvedValue([
      contract({ prepaidRentAmount: 0, remainingPrepaidCredit: 0, securityDepositAmount: 3_000_000 }),
    ])
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
    expect(screen.getByText("Tiền cọc (hoàn lại khi trả phòng)")).toBeTruthy()
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

  it("allows Manager to record move-out handover, enabling chargeAmount only for Damaged/Missing when moveIn was Good/Worn (C14)", async () => {
    mocks.getContractHandovers.mockResolvedValue({
      contractId: 12,
      moveIn: {
        handoverRecordId: 1,
        kind: "MoveIn",
        handoverDate: "2026-11-01",
        note: "Nhận phòng đủ đồ",
        recordedByName: "Manager A",
        recordedAtUtc: "2026-11-01T02:00:00Z",
        totalCharge: 0,
        items: [
          {
            handoverItemId: 1,
            equipmentId: 501,
            equipmentName: "Máy lạnh",
            quantity: 1,
            compensationValue: 8_500_000,
            condition: "Good",
            chargeAmount: 0,
            note: null,
            moveInCondition: null,
          },
          {
            handoverItemId: 2,
            equipmentId: 502,
            equipmentName: "Bàn học",
            quantity: 2,
            compensationValue: 500_000,
            condition: "Damaged",
            chargeAmount: 0,
            note: "Trầy sẵn",
            moveInCondition: null,
          },
        ],
      },
      moveOut: null,
      roomEquipments: [
        { equipmentId: 501, name: "Máy lạnh", quantity: 1, compensationValue: 8_500_000 },
        { equipmentId: 502, name: "Bàn học", quantity: 2, compensationValue: 500_000 },
      ],
    })
    mocks.saveContractMoveOutHandover.mockResolvedValue({
      contractId: 12,
      moveIn: null,
      moveOut: {
        handoverRecordId: 2,
        kind: "MoveOut",
        handoverDate: "2026-11-15",
        note: "Biên bản trả phòng",
        recordedByName: "Manager A",
        recordedAtUtc: "2026-11-15T03:00:00Z",
        totalCharge: 1_200_000,
        items: [
          {
            handoverItemId: 3,
            equipmentId: 501,
            equipmentName: "Máy lạnh",
            quantity: 1,
            compensationValue: 8_500_000,
            condition: "Damaged",
            chargeAmount: 1_200_000,
            note: "Vỡ mặt nạ",
            moveInCondition: "Good",
          },
          {
            handoverItemId: 4,
            equipmentId: 502,
            equipmentName: "Bàn học",
            quantity: 2,
            compensationValue: 500_000,
            condition: "Damaged",
            chargeAmount: 0,
            note: "",
            moveInCondition: "Damaged",
          },
        ],
      },
      roomEquipments: [],
    })

    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Xem" }))
    await screen.findByText("Biên bản bàn giao")

    fireEvent.click(await screen.findByRole("button", { name: "Lập biên bản trả phòng" }))

    const acChargeInput = screen.getByLabelText("Tiền bồi thường Máy lạnh") as HTMLInputElement
    const deskChargeInput = screen.getByLabelText("Tiền bồi thường Bàn học") as HTMLInputElement

    // Máy lạnh đang mặc định Tốt -> ô tiền bồi thường bị khóa
    expect(acChargeInput.disabled).toBe(true)
    // Bàn học lúc nhận phòng đã Damaged -> ô tiền bồi thường luôn khóa dù trả phòng là Damaged
    expect(deskChargeInput.disabled).toBe(true)

    // Đổi Máy lạnh sang Hao mòn -> vẫn khóa (hao mòn tự nhiên không bồi thường)
    fireEvent.change(screen.getByLabelText("Tình trạng Máy lạnh"), { target: { value: "Worn" } })
    expect(acChargeInput.disabled).toBe(true)

    // Đổi Máy lạnh sang Hư hỏng -> mở ô tiền bồi thường
    fireEvent.change(screen.getByLabelText("Tình trạng Máy lạnh"), { target: { value: "Damaged" } })
    expect(acChargeInput.disabled).toBe(false)

    // Nhập vượt trần (9.000.000 > 8.500.000) -> chặn phía client
    fireEvent.change(acChargeInput, { target: { value: "9000000" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu biên bản" }))
    expect(await screen.findByText("Máy lạnh: tiền bồi thường tối đa 8.500.000đ (1 × 8.500.000đ).")).toBeTruthy()
    expect(mocks.saveContractMoveOutHandover).not.toHaveBeenCalled()

    // Nhập hợp lệ 1.200.000 -> gửi thành công
    fireEvent.change(acChargeInput, { target: { value: "1200000" } })
    fireEvent.change(screen.getByLabelText("Ghi chú Máy lạnh"), { target: { value: "Vỡ mặt nạ" } })
    fireEvent.click(screen.getByRole("button", { name: "Lưu biên bản" }))

    await waitFor(() =>
      expect(mocks.saveContractMoveOutHandover).toHaveBeenCalledWith("manager-token", 12, {
        handoverDate: expect.stringMatching(/^\d{4}-\d{2}-\d{2}$/),
        note: null,
        items: [
          { equipmentId: 501, condition: "Damaged", chargeAmount: 1_200_000, note: "Vỡ mặt nạ" },
          { equipmentId: 502, condition: "Damaged", chargeAmount: 0, note: null },
        ],
      }),
    )
    expect(await screen.findByText(/Tổng bồi thường: 1\.200\.000/)).toBeTruthy()
  })

  it("renders 3 evidence sections with 7-day move-in deadline and uploads multiple files sequentially with progress (C15)", async () => {
    let resolveFirstUpload!: (value: unknown) => void
    const firstUploadPromise = new Promise((resolve) => {
      resolveFirstUpload = resolve
    })

    mocks.uploadContractEvidence
      .mockImplementationOnce(() => firstUploadPromise)
      .mockResolvedValueOnce({
        contractId: 12,
        moveInUploadDeadline: "2026-11-27",
        canUploadMoveIn: true,
        canUploadMoveOut: true,
        canUploadSignedContract: true,
        files: [
          {
            contractEvidenceFileId: 101,
            kind: "MoveIn",
            fileName: "anh-1.jpg",
            contentType: "image/jpeg",
            sizeBytes: 500_000,
            uploadedByName: "Manager A",
            uploadedByRole: "Manager",
            uploadedAtUtc: "2026-11-20T08:30:00Z",
            canDelete: true,
            url: "https://example.com/sign/anh-1.jpg",
            isHidden: false,
            hiddenReason: null,
          },
          {
            contractEvidenceFileId: 102,
            kind: "MoveIn",
            fileName: "anh-2.png",
            contentType: "image/png",
            sizeBytes: 600_000,
            uploadedByName: "Manager A",
            uploadedByRole: "Manager",
            uploadedAtUtc: "2026-11-20T08:31:00Z",
            canDelete: true,
            url: "https://example.com/sign/anh-2.png",
            isHidden: false,
            hiddenReason: null,
          },
        ],
      })

    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Xem" }))

    expect(await screen.findByRole("region", { name: "Ảnh nhận phòng" })).toBeTruthy()
    expect(screen.getByRole("region", { name: "Ảnh trả phòng" })).toBeTruthy()
    expect(screen.getByRole("region", { name: "Hợp đồng đã ký" })).toBeTruthy()
    expect(
      screen.getByText(
        /Bạn có thể thêm hoặc xóa ảnh nhận phòng đến hết ngày 27\/11\/2026 \(2026-11-27\) — trong vòng 7 ngày kể từ ngày nhận phòng\./,
      ),
    ).toBeTruthy()

    const file1 = new File(["img1"], "anh-1.jpg", { type: "image/jpeg" })
    const file2 = new File(["img2"], "anh-2.png", { type: "image/png" })
    const moveInInput = screen.getByLabelText("Thêm ảnh Ảnh nhận phòng")

    fireEvent.change(moveInInput, { target: { files: [file1, file2] } })

    // Trong khi file 1 đang upload, hiển thị tiến độ 1/2 và chưa gọi file 2
    expect(await screen.findByText("Đang tải lên 1/2: anh-1.jpg")).toBeTruthy()
    expect(mocks.uploadContractEvidence).toHaveBeenCalledTimes(1)
    expect(mocks.uploadContractEvidence).toHaveBeenNthCalledWith(
      1,
      "manager-token",
      12,
      "MoveIn",
      file1,
    )

    resolveFirstUpload({
      contractId: 12,
      moveInUploadDeadline: "2026-11-27",
      canUploadMoveIn: true,
      canUploadMoveOut: true,
      canUploadSignedContract: true,
      files: [
        {
          contractEvidenceFileId: 101,
          kind: "MoveIn",
          fileName: "anh-1.jpg",
          contentType: "image/jpeg",
          sizeBytes: 500_000,
          uploadedByName: "Manager A",
          uploadedByRole: "Manager",
          uploadedAtUtc: "2026-11-20T08:30:00Z",
          canDelete: true,
          url: "https://example.com/sign/anh-1.jpg",
          isHidden: false,
          hiddenReason: null,
        },
      ],
    })

    await waitFor(() =>
      expect(mocks.uploadContractEvidence).toHaveBeenCalledTimes(2),
    )
    expect(mocks.uploadContractEvidence).toHaveBeenNthCalledWith(
      2,
      "manager-token",
      12,
      "MoveIn",
      file2,
    )
    expect(
      await screen.findByText("Đã tải lên 2 file minh chứng thành công."),
    ).toBeTruthy()
    expect(screen.getByText("anh-1.jpg")).toBeTruthy()
    expect(screen.getByText("anh-2.png")).toBeTruthy()
  })

  it("allows Admin to hide an evidence file with a mandatory reason and shows hidden state (C15)", async () => {
    mocks.role = "ADMIN"
    mocks.getContractEvidence.mockResolvedValue({
      contractId: 12,
      moveInUploadDeadline: "2026-11-27",
      canUploadMoveIn: false,
      canUploadMoveOut: false,
      canUploadSignedContract: false,
      files: [
        {
          contractEvidenceFileId: 3,
          kind: "MoveIn",
          fileName: "vet-nut-tuong.png",
          contentType: "image/png",
          sizeBytes: 812_345,
          uploadedByName: "Nguyễn Văn A",
          uploadedByRole: "Customer",
          uploadedAtUtc: "2026-11-20T09:00:00Z",
          canDelete: false,
          url: "https://example.com/sign/vet-nut-tuong.png",
          isHidden: false,
          hiddenReason: null,
        },
      ],
    })
    mocks.hideContractEvidence.mockResolvedValue({
      contractId: 12,
      moveInUploadDeadline: "2026-11-27",
      canUploadMoveIn: false,
      canUploadMoveOut: false,
      canUploadSignedContract: false,
      files: [
        {
          contractEvidenceFileId: 3,
          kind: "MoveIn",
          fileName: "vet-nut-tuong.png",
          contentType: "image/png",
          sizeBytes: 812_345,
          uploadedByName: "Nguyễn Văn A",
          uploadedByRole: "Customer",
          uploadedAtUtc: "2026-11-20T09:00:00Z",
          canDelete: false,
          url: "https://example.com/sign/vet-nut-tuong.png",
          isHidden: true,
          hiddenReason: "Ảnh nhầm phòng khác",
        },
      ],
    })

    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Xem" }))

    const hideButton = await screen.findByRole("button", { name: "Ẩn" })
    // Admin không có nút Thêm ảnh
    expect(screen.queryByLabelText("Thêm ảnh Ảnh nhận phòng")).toBeNull()

    fireEvent.click(hideButton)
    const form = screen.getByRole("form", { name: "Biểu mẫu ẩn file minh chứng" })
    fireEvent.click(within(form).getByRole("button", { name: "Xác nhận ẩn" }))
    expect(await screen.findByText("Vui lòng nhập lý do ẩn minh chứng.")).toBeTruthy()
    expect(mocks.hideContractEvidence).not.toHaveBeenCalled()

    fireEvent.change(within(form).getByLabelText("Lý do ẩn"), {
      target: { value: "Ảnh nhầm phòng khác" },
    })
    fireEvent.click(within(form).getByRole("button", { name: "Xác nhận ẩn" }))

    await waitFor(() =>
      expect(mocks.hideContractEvidence).toHaveBeenCalledWith(
        "manager-token",
        12,
        3,
        "Ảnh nhầm phòng khác",
      ),
    )
    expect(await screen.findByText("Đã ẩn: Ảnh nhầm phòng khác")).toBeTruthy()
  })

  it("shows 30-day deposit forfeiture red warning and latest notice date before submitting move-out notice, and supports changing/canceling (C16)", async () => {
    const today = getVietnamTodayIsoDate()
    const nearDate = addIsoDays(today, 10)
    const expectedLatestNotice = addIsoDays(nearDate, -30)

    mocks.saveContractMoveOutNotice.mockResolvedValue(
      contract({
        plannedMoveOutDate: nearDate,
        moveOutNoticeAtUtc: new Date().toISOString(),
        moveOutNoticeDays: 10,
        moveOutForfeitsDeposit: true,
      }),
    )
    mocks.cancelContractMoveOutNotice.mockResolvedValue(
      contract({
        plannedMoveOutDate: null,
        moveOutNoticeAtUtc: null,
        moveOutNoticeDays: null,
        moveOutForfeitsDeposit: false,
      }),
    )

    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Xem" }))

    fireEvent.click(await screen.findByRole("button", { name: "Báo trả phòng" }))

    const dateInput = screen.getByLabelText("Ngày dự kiến trả phòng")
    fireEvent.change(dateInput, { target: { value: nearDate } })

    expect(
      await screen.findByText(
        `Bạn trả phòng trước hạn hợp đồng mà báo trước dưới 30 ngày nên sẽ mất phần tiền cọc còn lại khi quyết toán. Báo muộn nhất ngày ${formatContractDate(expectedLatestNotice)} để giữ cọc.`,
      ),
    ).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Xác nhận báo trả phòng" }))

    await waitFor(() =>
      expect(mocks.saveContractMoveOutNotice).toHaveBeenCalledWith(
        "manager-token",
        12,
        { plannedMoveOutDate: nearDate },
      ),
    )

    expect(
      await screen.findByText(
        new RegExp(
          `Dự kiến trả phòng ${formatContractDate(nearDate)} · báo trước 10 ngày`,
        ),
      ),
    ).toBeTruthy()
    expect(
      screen.getByText("Báo trước dưới 30 ngày — Sẽ mất phần cọc còn lại"),
    ).toBeTruthy()
    expect(screen.getByRole("button", { name: "Đổi ngày" })).toBeTruthy()

    fireEvent.click(screen.getByRole("button", { name: "Hủy báo trả phòng" }))

    await waitFor(() =>
      expect(mocks.cancelContractMoveOutNotice).toHaveBeenCalledWith(
        "manager-token",
        12,
      ),
    )
    expect(await screen.findByText("Đã hủy báo trả phòng.")).toBeTruthy()
  })

  it("disables terminate button until deposit settlement is confirmed, renders settlement preview table, and locks move-out edits after settlement (C17)", async () => {
    mocks.getContracts.mockResolvedValue([
      contract({
        securityDepositAmount: 3_000_000,
        prepaidRentAmount: 0,
        prepaidRentApplied: 0,
        remainingPrepaidCredit: 0,
      }),
    ])
    mocks.getContractDepositSettlement.mockResolvedValue({
      contractId: 12,
      isSettled: false,
      moveOutDate: "2026-11-15",
      contractEndDate: "2027-11-01",
      depositAmount: 3_000_000,
      damageCharges: 1_200_000,
      damageCoveredByDeposit: 1_200_000,
      outstandingDebt: 500_000,
      retainedForDebt: 500_000,
      forfeitedAmount: 1_300_000,
      refundAmount: 0,
      amountDue: 0,
      earlyWithoutNotice: true,
      noticeDays: 15,
      note: null,
      settledByName: null,
      settledAtUtc: null,
      refundConfirmedAtUtc: null,
      refundConfirmedByName: null,
      dueCollectedAtUtc: null,
      dueCollectedByName: null,
    })
    mocks.confirmContractDepositSettlement.mockResolvedValue({
      contractId: 12,
      isSettled: true,
      moveOutDate: "2026-11-15",
      contractEndDate: "2027-11-01",
      depositAmount: 3_000_000,
      damageCharges: 1_200_000,
      damageCoveredByDeposit: 1_200_000,
      outstandingDebt: 500_000,
      retainedForDebt: 500_000,
      forfeitedAmount: 1_300_000,
      refundAmount: 0,
      amountDue: 0,
      earlyWithoutNotice: true,
      noticeDays: 15,
      note: "Đã thống nhất quyết toán",
      settledByName: "Manager A",
      settledAtUtc: "2026-11-15T10:00:00Z",
      refundConfirmedAtUtc: null,
      refundConfirmedByName: null,
      dueCollectedAtUtc: null,
      dueCollectedByName: null,
    })

    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    const terminateBtn = screen.getByRole("button", {
      name: "Xác nhận kết thúc / Trả phòng",
    }) as HTMLButtonElement
    // Chưa quyết toán cọc -> nút Trả phòng bị khóa
    expect(terminateBtn.disabled).toBe(true)

    fireEvent.click(screen.getByRole("button", { name: "Xem" }))

    const settlementSection = await screen.findByRole("region", {
      name: "Quyết toán cọc hợp đồng 12",
    })
    expect(
      within(settlementSection).getByText("Bản xem trước (Chưa quyết toán)"),
    ).toBeTruthy()
    expect(
      within(settlementSection).getByText(
        "trả trước hạn, báo trước 15 ngày < 30",
      ),
    ).toBeTruthy()
    expect(within(settlementSection).getByText("Hoàn lại")).toBeTruthy()
    expect(within(settlementSection).getByText("Khách nộp thêm")).toBeTruthy()

    // Bấm Xác nhận quyết toán khi còn nợ nhưng chưa tick -> chặn và báo lỗi
    fireEvent.click(
      within(settlementSection).getByRole("button", {
        name: "Xác nhận quyết toán",
      }),
    )
    expect(
      await within(settlementSection).findByRole("alert"),
    ).toBeTruthy()
    expect(mocks.confirmContractDepositSettlement).not.toHaveBeenCalled()

    // Tick "Khách không trả, giữ cọc bù nợ", nhập ghi chú và xác nhận trong hộp thoại
    fireEvent.click(
      within(settlementSection).getByLabelText("Khách không trả, giữ cọc bù nợ"),
    )
    fireEvent.change(
      within(settlementSection).getByLabelText("Ghi chú quyết toán"),
      { target: { value: "Đã thống nhất quyết toán" } },
    )
    fireEvent.click(
      within(settlementSection).getByRole("button", {
        name: "Xác nhận quyết toán",
      }),
    )

    const confirmDialog = await screen.findByRole("dialog", {
      name: "Hộp xác nhận quyết toán cọc",
    })
    fireEvent.click(
      within(confirmDialog).getByRole("button", { name: "Đồng ý quyết toán" }),
    )

    await waitFor(() =>
      expect(mocks.confirmContractDepositSettlement).toHaveBeenCalledWith(
        "manager-token",
        12,
        {
          outstandingDebtAcknowledged: true,
          note: "Đã thống nhất quyết toán",
        },
      ),
    )

    expect(
      await within(settlementSection).findByText("Đã quyết toán"),
    ).toBeTruthy()
    // Sau khi quyết toán cọc -> nút Trả phòng được bật, ẩn nút Lập/Cập nhật biên bản trả phòng và Thêm ảnh trả phòng
    expect(terminateBtn.disabled).toBe(false)
    expect(
      screen.queryByRole("button", { name: "Lập biên bản trả phòng" }),
    ).toBeNull()
    expect(screen.queryByLabelText("Thêm ảnh Ảnh trả phòng")).toBeNull()
  })

  it("allows Manager to confirm deposit refund and due collected via C11-style confirmation dialogs after settlement (C18)", async () => {
    const settledBase = {
      contractId: 12,
      isSettled: true,
      moveOutDate: "2026-11-15",
      contractEndDate: "2027-11-01",
      depositAmount: 3_000_000,
      damageCharges: 1_200_000,
      damageCoveredByDeposit: 1_200_000,
      outstandingDebt: 0,
      retainedForDebt: 0,
      forfeitedAmount: 0,
      refundAmount: 1_800_000,
      amountDue: 1_000_000,
      earlyWithoutNotice: false,
      noticeDays: 40,
      note: null,
      settledByName: "Manager A",
      settledAtUtc: "2026-11-15T10:00:00Z",
      refundConfirmedAtUtc: null as string | null,
      refundConfirmedByName: null as string | null,
      dueCollectedAtUtc: null as string | null,
      dueCollectedByName: null as string | null,
    }

    mocks.getContracts.mockResolvedValue([
      contract({
        securityDepositAmount: 3_000_000,
        prepaidRentAmount: 0,
        prepaidRentApplied: 0,
        remainingPrepaidCredit: 0,
      }),
    ])
    mocks.getContractDepositSettlement.mockResolvedValue(settledBase)
    mocks.confirmDepositSettlementRefund.mockResolvedValue({
      ...settledBase,
      refundConfirmedAtUtc: "2026-11-15T11:00:00Z",
      refundConfirmedByName: "Manager A",
    })
    mocks.confirmDepositSettlementDueCollected.mockResolvedValue({
      ...settledBase,
      refundConfirmedAtUtc: "2026-11-15T11:00:00Z",
      refundConfirmedByName: "Manager A",
      dueCollectedAtUtc: "2026-11-15T11:15:00Z",
      dueCollectedByName: "Manager A",
    })

    render(
      <MemoryRouter initialEntries={["/management/contracts"]}>
        <ManagementContractsPage />
      </MemoryRouter>,
    )

    await screen.findByText("HĐ #12")
    fireEvent.click(screen.getByRole("button", { name: "Xem" }))

    const settlementSection = await screen.findByRole("region", {
      name: "Quyết toán cọc hợp đồng 12",
    })

    // 1. Xác nhận "Đã hoàn cọc"
    expect(
      await within(settlementSection).findByText(/Chờ hoàn 1\.800\.000/),
    ).toBeTruthy()
    fireEvent.click(
      within(settlementSection).getByRole("button", { name: "Đã hoàn cọc" }),
    )

    const refundDialog = await screen.findByRole("dialog", {
      name: "Xác nhận đã hoàn cọc",
    })
    expect(
      within(refundDialog).getByText(
        "Chỉ xác nhận sau khi bạn ĐÃ chuyển tiền cho khách. Hệ thống không chuyển tiền.",
      ),
    ).toBeTruthy()
    fireEvent.click(
      within(refundDialog).getByRole("button", { name: "Xác nhận đã hoàn" }),
    )

    await waitFor(() =>
      expect(mocks.confirmDepositSettlementRefund).toHaveBeenCalledWith(
        "manager-token",
        12,
      ),
    )
    expect(
      await within(settlementSection).findByText(/Đã hoàn lúc .* — Manager A/),
    ).toBeTruthy()

    // 2. Xác nhận "Đã thu"
    expect(
      within(settlementSection).getByText(/Khách cần nộp thêm 1\.000\.000/),
    ).toBeTruthy()
    fireEvent.click(
      within(settlementSection).getByRole("button", { name: "Đã thu" }),
    )

    const dueDialog = await screen.findByRole("dialog", {
      name: "Xác nhận đã thu",
    })
    expect(
      within(dueDialog).getByText(
        "Chỉ xác nhận sau khi bạn ĐÃ nhận tiền của khách. Hệ thống không chuyển tiền.",
      ),
    ).toBeTruthy()
    fireEvent.click(
      within(dueDialog).getByRole("button", { name: "Xác nhận đã thu" }),
    )

    await waitFor(() =>
      expect(mocks.confirmDepositSettlementDueCollected).toHaveBeenCalledWith(
        "manager-token",
        12,
      ),
    )
    expect(
      await within(settlementSection).findByText(/Đã thu lúc .* — Manager A/),
    ).toBeTruthy()
  })
})
