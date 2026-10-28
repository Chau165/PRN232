import { describe, expect, it, vi } from "vitest"
import { createInvoiceComplaint, getInvoiceComplaints, updateInvoiceComplaintStatus } from "@/api/invoiceComplaintsApi"
import { complaintStatusDisplay } from "@/utils/complaintDisplay"

function response(body: unknown) {
  return new Response(JSON.stringify(body), { status: 200, headers: { "Content-Type": "application/json" } })
}

describe("Invoice complaint API client", () => {
  it("uses the invoice-complaints routes with the API field names", async () => {
    const fetchMock = vi.spyOn(globalThis, "fetch").mockImplementation(async () => response({}))
    await getInvoiceComplaints("token")
    await createInvoiceComplaint("token", { invoiceId: 5, feeType: "Tiền phòng tháng 10/2026", content: "Sai giá" })
    await updateInvoiceComplaintStatus("token", 3, "Resolved")

    expect(fetchMock.mock.calls.map(([url, init]) => `${init?.method ?? "GET"} ${String(url).replace(/^.*\/api/, "/api")}`)).toEqual([
      "GET /api/invoice-complaints",
      "POST /api/invoice-complaints",
      "PATCH /api/invoice-complaints/3",
    ])
    expect(JSON.parse(String(fetchMock.mock.calls[1][1]?.body)))
      .toEqual({ invoiceId: 5, feeType: "Tiền phòng tháng 10/2026", content: "Sai giá" })
    expect(JSON.parse(String(fetchMock.mock.calls[2][1]?.body))).toEqual({ status: "Resolved" })
  })

  it("labels complaint statuses in Vietnamese", () => {
    expect(complaintStatusDisplay("New")).toEqual({ label: "Mới", tone: "amber" })
    expect(complaintStatusDisplay("InProgress").label).toBe("Đang xử lý")
    expect(complaintStatusDisplay("Resolved")).toEqual({ label: "Đã xử lý", tone: "green" })
  })
})
