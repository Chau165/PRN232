export type LatestBillSummary = {
  period: string
  total: string
  due: string
  status: "CHƯA THANH TOÁN" | "ĐÃ THANH TOÁN"
}

export type RoomRental = {
  propertyName: string
  propertyAddress: string
  roomCode: string
  roomSize: string
  monthlyRent: string
  contractStart: string
  contractEnd: string
  occupants: string
  equipment: string[]
  images: string[]
  latestBill: LatestBillSummary
}

export type ContractStatus = "Đang hiệu lực" | "Đã kết thúc"

export type Contract = {
  id: string
  propertyName: string
  roomCode: string
  tenantName: string
  startDate: string
  endDate: string
  monthlyRent: string
  deposit: string
  status: ContractStatus
}

export type InvoiceStatus = "CHƯA THANH TOÁN" | "ĐÃ THANH TOÁN"

export type InvoiceItem = {
  label: string
  amount: string
}

export type Invoice = {
  id: string
  title: string
  period: string
  items: InvoiceItem[]
  total: string
  due: string
  status: InvoiceStatus
}

export type Payment = {
  id: string
  invoiceId: string
  title: string
  paidAt: string
  amount: string
  method: string
  status: "Thành công" | "Đang xử lý"
}

export type MaintenanceStatus = "ĐÃ GỬI" | "ĐÃ TIẾP NHẬN" | "ĐANG XỬ LÝ" | "HOÀN THÀNH"

export type MaintenancePriority = "Bình thường" | "Quan trọng" | "Khẩn cấp"

export type MaintenanceTimelineItem = {
  label: string
  date?: string
  done: boolean
}

export type MaintenanceRequest = {
  id: string
  roomCode: string
  propertyId?: string
  customerName?: string
  category: string
  relatedEquipment?: string
  equipmentName?: string
  title: string
  description: string
  priority: MaintenancePriority
  status: MaintenanceStatus
  createdAt: string
  images?: string[]
  warrantyStatus?: string
  assignedTo?: string
  internalNotes?: string
  repairCost?: string
  timeline: MaintenanceTimelineItem[]
  imageName?: string
  rating?: number
}

export type MaintenanceRequestForm = {
  category: string
  relatedEquipment?: string
  title: string
  description: string
  priority: MaintenancePriority
  imageName?: string
}

export type BookingStatus = "Pending" | "Deposit Paid" | "Confirmed" | "Cancelled" | "Converted to Rental"

export type Booking = {
  id: string
  propertyName: string
  roomCode: string
  viewingDate: string
  depositAmount: string
  paymentStatus: "Chưa thanh toán" | "Đã thanh toán" | "Hoàn tiền"
  bookingStatus: BookingStatus
}

export type Notification = {
  id: string
  title: string
  body: string
  date: string
  read: boolean
  href?: string
}

export type Profile = {
  fullName: string
  email: string
  phone: string
}
