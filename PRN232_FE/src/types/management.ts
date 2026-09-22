import type {
  BookingStatus,
  ContractStatus,
  InvoiceStatus,
  MaintenancePriority,
  MaintenanceStatus,
} from "@/types/customer"

export type ManagedPropertyStatus = "Đang hoạt động" | "Tạm đóng"

export type ManagedProperty = {
  id: string
  name: string
  area: string
  address: string
  image: string
  totalRooms: number
  availableRooms: number
  occupiedRooms: number
  maintenanceRooms: number
  managerId?: string
  managerName?: string
  status: ManagedPropertyStatus
}

export type ManagedRoomStatus = "Trống" | "Đã đặt cọc" | "Đang thuê" | "Đang sửa chữa"

export type ManagedRoom = {
  id: string
  propertyId: string
  code: string
  price: string
  tenantName?: string
  tenantId?: string
  occupancy: string
  status: ManagedRoomStatus
  equipmentCount: number
}

export type Tenant = {
  id: string
  name: string
  phone: string
  email: string
  propertyId: string
  propertyName: string
  roomCode: string
  contractId: string
  paymentStatus: "Đúng hạn" | "Còn nợ"
}

export type ManagedBooking = {
  id: string
  customerName: string
  customerId?: string
  customerPhone?: string
  companions?: { name: string; phone?: string }[]
  moveInDate?: string
  propertyId: string
  propertyName: string
  roomCode: string
  viewingDate: string
  occupants: string
  deposit: string
  paymentStatus: "Chưa thanh toán" | "Đã thanh toán" | "Hoàn tiền"
  status: BookingStatus
}

export type ManagedContract = {
  id: string
  bookingId?: string
  tenantPhone?: string
  companions?: { name: string; phone?: string }[]
  tenantId: string
  tenantName: string
  propertyId: string
  propertyName: string
  roomCode: string
  startDate: string
  endDate: string
  monthlyRent: string
  deposit: string
  status: ContractStatus
}

export type ManagedInvoice = {
  id: string
  propertyId: string
  propertyName: string
  roomCode: string
  tenantName: string
  period: string
  roomRent: string
  electricity: string
  water: string
  internet: string
  otherFees: string
  total: string
  due: string
  status: "DRAFT" | "UNPAID" | "PAID" | "OVERDUE"
  electricityUsage?: number
  electricityRate?: number
  waterUsage?: number
  waterRate?: number
  parkingFee?: string
  trashFee?: string
  additionalFees?: InvoiceAdditionalFee[]
}

export type InvoiceAdditionalFee = {
  name: string
  amount: string
}

export type InvoiceComplaintStatus = "MỚI" | "ĐANG XỬ LÝ" | "ĐÃ XỬ LÝ"

export type ManagedInvoiceComplaint = {
  id: string
  invoiceId: string
  propertyId: string
  propertyName: string
  roomCode: string
  tenantName: string
  feeType: string
  content: string
  createdAt: string
  status: InvoiceComplaintStatus
}

export type Equipment = {
  id: string
  name: string
  roomCode: string
  propertyId: string
  propertyName: string
  installedDate: string
  warrantyEnd: string
  supplier: string
  status: "Đang sử dụng" | "Đang sửa chữa" | "Ngưng sử dụng"
}

export type ManagedMaintenanceRequest = {
  id: string
  propertyId: string
  propertyName: string
  roomCode: string
  customerName: string
  equipmentName: string
  problem: string
  description: string
  images: string[]
  warrantyStatus: string
  priority: MaintenancePriority
  createdDate: string
  assignedTo: string
  internalNotes: string
  repairCost: string
  status: MaintenanceStatus
}

export type Area = {
  id: string
  name: string
  propertyIds: string[]
  managerNames: string[]
  status: "Đang hoạt động" | "Tạm đóng"
}

export type Manager = {
  id: string
  name: string
  email: string
  phone: string
  assignedPropertyIds: string[]
  status: "Đang hoạt động" | "Tạm khóa"
}

export type ManagedUser = {
  id: string
  name: string
  email: string
  role: "ADMIN" | "MANAGER" | "CUSTOMER"
  status: "Đang hoạt động" | "Tạm khóa"
}
