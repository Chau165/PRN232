import { describe, expect, it, vi } from "vitest"
import { createInvoice, getInvoices, payInvoice, previewInvoice, recordCashPayment } from "@/services/invoiceService"
import { createUtilityReading, getUtilityReadings } from "@/services/utilityReadingService"
import { defaultDueDate, formatMonth, invoiceLineLabel, invoiceStatusDisplay } from "@/utils/invoiceDisplay"

function response(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } })
}

describe("Invoice and meter reading API clients", () => {
  it("uses the invoice and utility-reading routes with the API field names", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({}))
    const request = { contractId: 5, invoiceMonth: "2026-10-01", dueDate: "2026-11-10" }
    await getInvoices("token")
    await previewInvoice("token", request)
    await createInvoice("token", request)
    await getUtilityReadings("token", 5)
    await createUtilityReading("token", {
      contractId: 5, readingMonth: "2026-10-01", electricEnd: 150, waterEnd: 14, electricStart: null, waterStart: null,
    })

    expect(fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? "GET"} ${String(url).replace(/^.*\/api/, "/api")}`)).toEqual([
      "GET /api/invoices",
      "POST /api/invoices/preview",
      "POST /api/invoices",
      "GET /api/utility-readings?contractId=5",
      "POST /api/utility-readings",
    ])
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body))).toEqual(request)
    expect(JSON.parse(String(fetchMock.mock.calls[4][1]?.body))).toEqual({
      contractId: 5, readingMonth: "2026-10-01", electricEnd: 150, waterEnd: 14, electricStart: null, waterStart: null,
    })
  })
})

describe("Invoice payment API clients", () => {
  it("sends only the payment method; the server decides the amount", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({}))
    await payInvoice("token", 5, "MoMo")
    await recordCashPayment("token", 5)

    expect(fetchMock.mock.calls.map(([url, init]) => `${init?.method} ${String(url).replace(/^.*\/api/, "/api")}`))
      .toEqual(["POST /api/invoices/5/pay", "POST /api/invoices/5/record-cash"])
    expect(JSON.parse(String(fetchMock.mock.calls[0][1]?.body))).toEqual({ paymentMethod: "MoMo" })
    expect(fetchMock.mock.calls[1][1]?.body).toBeUndefined()
  })
})

describe("Invoice display helpers", () => {
  it("derives overdue from the due date and labels each status", () => {
    const label = (status: string, dueDate: string) => invoiceStatusDisplay({ status, dueDate }, "2026-10-29").label
    expect(label("Unpaid", "2026-11-10")).toBe("Chưa thanh toán")
    expect(label("Unpaid", "2026-10-28")).toBe("Quá hạn")
    expect(label("Overdue", "2026-11-10")).toBe("Quá hạn")
    expect(label("Paid", "2026-10-01")).toBe("Đã thanh toán")
  })

  it("describes metered lines with quantity and unit price", () => {
    const line = { itemType: "Electricity", description: "Electricity - per kWh - tháng 10/2026", quantity: 50, unitPrice: 3500, unitCode: "kwh", amount: 175000 }
    expect(invoiceLineLabel(line)).toMatch(/^Điện \(50 kWh × 3\.500/)
    expect(invoiceLineLabel({ ...line, itemType: "Rent", quantity: null, unitPrice: null, unitCode: null })).toBe("Tiền phòng")
    expect(invoiceLineLabel({ ...line, itemType: "Service", description: "Internet - monthly - tháng 10/2026" }))
      .toBe("Internet - monthly - tháng 10/2026")
    expect(formatMonth("2026-10-01")).toBe("10/2026")
  })

  it("defaults the due date to the 10th of the next month but never before today", () => {
    expect(defaultDueDate("2026-10-01", "2026-10-29")).toBe("2026-11-10")
    expect(defaultDueDate("2026-12-01", "2026-12-15")).toBe("2027-01-10")
    expect(defaultDueDate("2026-09-01", "2026-10-29")).toBe("2026-10-29")
  })
})
