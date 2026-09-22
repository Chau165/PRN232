import { managedInvoiceComplaints } from "@/data/management"
import type { InvoiceComplaintForm } from "@/types/customer"
import type {
  InvoiceComplaintStatus,
  ManagedInvoiceComplaint,
} from "@/types/management"

const storageKey = "troviet-invoice-complaints"

type CreateInvoiceComplaintInput = InvoiceComplaintForm & {
  propertyId: string
  propertyName: string
  roomCode: string
  tenantName: string
}

function getStoredComplaints(): ManagedInvoiceComplaint[] {
  try {
    const stored = window.localStorage.getItem(storageKey)
    return stored ? (JSON.parse(stored) as ManagedInvoiceComplaint[]) : []
  } catch {
    return []
  }
}

export function getInvoiceComplaints(): ManagedInvoiceComplaint[] {
  const storedComplaints = getStoredComplaints()
  const storedIds = new Set(storedComplaints.map((complaint) => complaint.id))
  return [
    ...storedComplaints,
    ...managedInvoiceComplaints.filter(
      (complaint) => !storedIds.has(complaint.id),
    ),
  ]
}

export function createInvoiceComplaint({
  invoiceId,
  feeType,
  content,
  propertyId,
  propertyName,
  roomCode,
  tenantName,
}: CreateInvoiceComplaintInput) {
  const complaint: ManagedInvoiceComplaint = {
    id: `KN-${Date.now()}`,
    invoiceId,
    propertyId,
    propertyName,
    roomCode,
    tenantName,
    feeType,
    content,
    createdAt: new Intl.DateTimeFormat("vi-VN").format(new Date()),
    status: "MỚI",
  }
  const complaints = [complaint, ...getStoredComplaints()]
  window.localStorage.setItem(storageKey, JSON.stringify(complaints))
  return complaint
}

export function updateInvoiceComplaintStatus(
  id: string,
  status: InvoiceComplaintStatus,
) {
  const complaint = getInvoiceComplaints().find(
    (item) => item.id === id,
  )
  if (!complaint) return null

  const updatedComplaint = { ...complaint, status }
  const storedComplaints = getStoredComplaints()
  const storedIndex = storedComplaints.findIndex((item) => item.id === id)
  const complaints =
    storedIndex >= 0
      ? storedComplaints.map((item) =>
          item.id === id ? updatedComplaint : item,
        )
      : [updatedComplaint, ...storedComplaints]

  window.localStorage.setItem(storageKey, JSON.stringify(complaints))
  return updatedComplaint
}
