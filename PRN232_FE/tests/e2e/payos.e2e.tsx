import { fireEvent, render, screen, waitFor, cleanup } from "@testing-library/react"
import { MemoryRouter, Route, Routes } from "react-router"
import { expect, it, vi } from "vitest"
import { AuthProvider } from "@/auth/AuthContext"
import { ProtectedRoute } from "@/components/auth/ProtectedRoute"
import PayInvoiceModal from "@/components/customer/PayInvoiceModal"
import InvoicePaymentResultPage from "@/pages/InvoicePaymentResultPage/InvoicePaymentResultPage"
import ManagementReportsPage from "@/pages/ManagementReportsPage/ManagementReportsPage"
import { getInvoices, recordCashPayment } from "@/services/invoiceService"
import { getPaymentRequest } from "@/services/invoicePaymentService"
import { recalledInvoicePayment } from "@/utils/invoicePayment"

const navigation = vi.hoisted(() => ({ open: vi.fn() }))
// Only external browser navigation is intercepted; auth, API clients and BE endpoints are real.
vi.mock("@/utils/invoicePayment", async importOriginal => ({
  ...await importOriginal<typeof import("@/utils/invoicePayment")>(), openPayOSCheckout: navigation.open,
}))
const origin = process.env.PAYOS_E2E_ORIGIN!
const token = process.env.PAYOS_E2E_CUSTOMER_TOKEN!
const manager = process.env.PAYOS_E2E_MANAGER_TOKEN!
const scenario = process.env.PAYOS_E2E_SCENARIO!
const control = (path: string) => fetch(origin + path, { method: "POST" })
const snapshot = async () => (await fetch(origin + "/__test/db")).json()

it(`verifies the full invoice payment chain: ${scenario}`, async () => {
  expect(origin).toMatch(/^http:\/\/127\.0\.0\.1:\d+$/)
  sessionStorage.clear()
  sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({ accessToken: token, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() }))
  const invoice = (await getInvoices(token)).find(item => item.invoiceId === Number(process.env.PAYOS_E2E_INVOICE_ID))!
  expect(invoice.status).toBe("Unpaid")
  const view = render(<AuthProvider><MemoryRouter initialEntries={["/pay"]}><Routes>
    <Route path="/pay" element={<ProtectedRoute roles={["CUSTOMER"]}><PayInvoiceModal invoice={invoice} onClose={() => {}} /></ProtectedRoute>} />
    <Route path="/payment-result" element={<ProtectedRoute roles={["CUSTOMER"]}><InvoicePaymentResultPage /></ProtectedRoute>} />
  </Routes></MemoryRouter></AuthProvider>)
  fireEvent.click(await screen.findByRole("button", { name: "Thanh toán qua payOS" }))
  await waitFor(() => expect(recalledInvoicePayment("10")).not.toBeNull(), { timeout: 10000 })
  const requestId = recalledInvoicePayment("10")!.requestId
  expect((await getPaymentRequest(token, requestId)).expiresAtUtc).toMatch(/Z$/)
  const createdState = await snapshot()
  if (scenario !== "recovery") expect(createdState.checkoutFailure).toBeNull()
  expect(createdState.paymentCount).toBe(0)
  expect(screen.queryByText("Thanh toán thành công")).toBeNull()

  if (scenario === "recovery") {
    await screen.findByRole("heading", { name: "Đang tạo liên kết thanh toán" })
    expect(navigation.open).not.toHaveBeenCalled()
    await control("/__test/due")
    const recovered = await fetch(`${origin}/api/payment-requests/${requestId}/reconcile`, { method: "POST", headers: { Authorization: `Bearer ${manager}` } })
    expect(recovered.status).toBe(200)
    expect((await recovered.json()).status).toBe("Pending")
    await control("/__test/pay/paid-without-webhook")
    await control("/__test/due")
    const settled = await fetch(`${origin}/api/payment-requests/${requestId}/reconcile`, { method: "POST", headers: { Authorization: `Bearer ${manager}` } })
    expect(settled.status).toBe(200)
    expect((await settled.json()).status).toBe("Succeeded")
  } else {
    await waitFor(() => expect(navigation.open).toHaveBeenCalledWith("https://pay.payos.vn/web/b12-test-link"))
    if (scenario === "cash") {
      expect((await recordCashPayment(manager, invoice.invoiceId)).status).toBe("Paid")
      expect((await snapshot()).cancels).toBe(1)
    } else {
      await control("/__test/pay/paid-without-webhook")
      expect((await control("/__test/pay/tamper")).status).toBe(400)
      expect((await snapshot()).paymentCount).toBe(0)
      expect((await control("/__test/pay/complete")).status).toBe(200)
      expect((await control("/__test/pay/complete")).status).toBe(200)
    }
  }
  view.unmount()
  render(<AuthProvider><MemoryRouter initialEntries={[`/payment-result?requestId=${requestId}&status=PAID&code=00`]}><ProtectedRoute roles={["CUSTOMER"]}><InvoicePaymentResultPage /></ProtectedRoute></MemoryRouter></AuthProvider>)
  await screen.findByRole("heading", { name: scenario === "cash" ? "Hóa đơn đã được thanh toán" : "Thanh toán thành công" })
  const state = await snapshot()
  expect(state.invoiceStatus).toBe("Paid")
  expect(state.paymentCount).toBe(1)
  expect(state.paymentMethod).toBe(scenario === "cash" ? "Cash" : "PayOS")
  expect(state.paidAtUtc).toBeTruthy()
  expect(state.creates).toBe(1)
  expect(state.amount).toBe(3355000) // includes rent, electricity, water, internet
  expect((await getInvoices(token)).find(item => item.invoiceId === invoice.invoiceId)!.status).toBe("Paid")
  expect((await getPaymentRequest(token, requestId)).invoiceStatus).toBe("Paid")

  // Simulate an additional bank transfer after an invoice was already paid: it must not inflate revenue.
  expect((await control("/__test/pay/extra")).status).toBe(200)
  expect((await control("/__test/pay/extra")).status).toBe(200)
  expect((await snapshot()).reviewCount).toBe(1)
  expect((await snapshot()).paymentCount).toBe(1)
  cleanup()
  sessionStorage.setItem("troviet-auth-session-v1", JSON.stringify({ accessToken: manager, expiresAtUtc: new Date(Date.now() + 3600000).toISOString() }))
  render(<AuthProvider><MemoryRouter><ProtectedRoute roles={["MANAGER"]}><ManagementReportsPage /></ProtectedRoute></MemoryRouter></AuthProvider>)
  await screen.findByText("1 giao dịch đã ghi nhận")
  expect(screen.getAllByText(/3\.355\.000/).length).toBeGreaterThanOrEqual(1)
  expect(screen.getByText(/0 hóa đơn · đến/)).toBeTruthy()
  expect(screen.queryByText("624,5 triệu")).toBeNull()
  expect(screen.getByText("Sunrise")).toBeTruthy()
}, 60000)
