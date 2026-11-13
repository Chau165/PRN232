import { act, render, screen } from "@testing-library/react"
import { RouterProvider } from "react-router"
import { afterAll, beforeEach, expect, it, vi } from "vitest"
const mocks = vi.hoisted(() => ({
  role: "CUSTOMER",
  get: vi.fn(),
  list: vi.fn(),
}))
vi.mock("@/auth/AuthContext", () => ({
  useAuth: () => ({
    status: "authenticated",
    accessToken: "token",
    user: {
      id: "10",
      role: mocks.role,
      name: "Test User",
      email: "test@example.test",
      phone: "0900",
    },
    logout: vi.fn(),
  }),
  useOptionalAuth: () => null,
}))
vi.mock("@/auth/CustomerNotificationsContext", () => ({
  CustomerNotificationsProvider: ({
    children,
  }: {
    children: React.ReactNode
  }) => children,
  useCustomerNotificationsState: () => ({
    notifications: [],
    unreadCount: 0,
    refresh: vi.fn(),
    markRead: vi.fn(),
  }),
}))
vi.mock("@/services/invoicePaymentService", () => ({
  getPaymentRequest: mocks.get,
}))
vi.mock("@/services/bookingRefundService", () => ({
  getRefundReviews: mocks.list,
}))
import { router } from "@/app/router"
import { setCurrentUserSnapshot } from "@/auth/currentUserStore"
afterAll(() => router.dispose())
beforeEach(() => {
  sessionStorage.clear()
  mocks.role = "CUSTOMER"
  mocks.list.mockResolvedValue([])
  mocks.get.mockResolvedValue({
    paymentRequestId: 12,
    invoiceId: null,
    invoiceStatus: null,
    bookingId: 7,
    bookingStatus: "Pending",
    amount: 3000000,
    currency: "VND",
    status: "Pending",
    isExpired: false,
    canOpenCheckout: false,
    checkoutUrl: null,
  })
})
it("registers the real callback route and verifies legacy success URLs rather than rendering unconditional success", async () => {
  await act(async () => {
    await router.navigate("/payment-success?requestId=12&status=PAID")
  })
  render(<RouterProvider router={router} />)
  await screen.findByRole("heading", { name: "Đang chờ xác nhận tiền cọc" })
  expect(router.state.location.pathname).toBe("/payment-result")
  expect(
    screen.queryByRole("heading", { name: "Thanh toán thành công" }),
  ).toBeNull()
})
it("protects the Admin refund route from Manager", async () => {
  mocks.role = "MANAGER"
  await act(async () => {
    await router.navigate("/management/refunds")
  })
  render(<RouterProvider router={router} />)
  await screen.findByRole("heading", { name: "Không có quyền truy cập" })
  expect(mocks.list).not.toHaveBeenCalled()
})
it("opens the admin refund screen through the real router", async () => {
  setCurrentUserSnapshot({
    id: "10",
    role: "ADMIN",
    name: "Test User",
    email: "test@example.test",
    phone: "0900",
  })
  mocks.role = "ADMIN"
  await act(async () => {
    await router.navigate("/management/refunds?view=admin")
  })
  render(<RouterProvider router={router} />)
  await screen.findByText("Không có đơn phù hợp.")
  expect(mocks.list).toHaveBeenCalledWith(
    "token",
    "PendingReview",
    1,
    expect.any(AbortSignal),
  )
})
