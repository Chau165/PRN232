export interface InvoiceLineDto {
  itemType: string
  description: string
  quantity: number | null
  unitPrice: number | null
  unitCode: string | null
  amount: number
}

export interface InvoiceDto {
  // Bản xem trước có invoiceId = 0 và status = "Preview".
  invoiceId: number
  contractId: number
  roomCode: string
  propertyName: string
  customerName: string
  invoiceMonth: string
  dueDate: string
  status: string
  total: number
  lines: InvoiceLineDto[]
}

export interface InvoiceRequest {
  contractId: number
  invoiceMonth: string
  dueDate: string
}
