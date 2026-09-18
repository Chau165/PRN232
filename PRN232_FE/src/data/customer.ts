import { rooms } from "@/data/rooms"
import type {
  Booking,
  Contract,
  Invoice,
  MaintenanceRequest,
  Notification,
  Payment,
  Profile,
  RoomRental,
} from "@/types/customer"

const rentalRoom = rooms[1]

export const currentRoom: RoomRental = {
  propertyName: "Sunrise Residence",
  propertyAddress: "Đường Võ Văn Ngân, Thủ Đức, TP.HCM",
  roomCode: "A203",
  roomSize: rentalRoom.size,
  monthlyRent: rentalRoom.price,
  contractStart: "01/08/2026",
  contractEnd: "31/07/2027",
  occupants: "Tối đa 2 người",
  equipment: ["Máy lạnh", "Bình nóng lạnh", "Giường", "Tủ"],
  images: [
    rentalRoom.image,
    "https://images.unsplash.com/photo-1616486338812-3dadae4b4ace?auto=format&fit=crop&w=900&q=85",
  ],
  latestBill: {
    period: "09/2026",
    total: "4.420.000đ",
    due: "05/10/2026",
    status: "CHƯA THANH TOÁN",
  },
}

export const contracts: Contract[] = [
  {
    id: "HD-2026-0083",
    propertyName: "Sunrise Residence",
    roomCode: "A203",
    tenantName: "Ngọc Anh",
    startDate: "01/08/2026",
    endDate: "31/07/2027",
    monthlyRent: "3.800.000đ",
    deposit: "7.600.000đ",
    status: "Đang hiệu lực",
  },
  {
    id: "HD-2025-0016",
    propertyName: "Nhà trọ Mộc Miên",
    roomCode: "B204",
    tenantName: "Ngọc Anh",
    startDate: "01/08/2025",
    endDate: "31/07/2026",
    monthlyRent: "2.800.000đ",
    deposit: "5.600.000đ",
    status: "Đã kết thúc",
  },
]

export const invoices: Invoice[] = [
  {
    id: "INV-09-2026",
    title: "Hóa đơn tháng 09/2026",
    period: "09/2026",
    items: [
      { label: "Tiền phòng", amount: "3.800.000đ" },
      { label: "Điện", amount: "350.000đ" },
      { label: "Nước", amount: "120.000đ" },
      { label: "Internet", amount: "100.000đ" },
      { label: "Phí khác", amount: "50.000đ" },
    ],
    total: "4.420.000đ",
    due: "05/10/2026",
    status: "CHƯA THANH TOÁN",
  },
  {
    id: "INV-08-2026",
    title: "Hóa đơn tháng 08/2026",
    period: "08/2026",
    items: [
      { label: "Tiền phòng", amount: "3.800.000đ" },
      { label: "Điện", amount: "310.000đ" },
      { label: "Nước", amount: "120.000đ" },
      { label: "Internet", amount: "100.000đ" },
      { label: "Phí khác", amount: "50.000đ" },
    ],
    total: "4.380.000đ",
    due: "05/09/2026",
    status: "ĐÃ THANH TOÁN",
  },
]

export const payments: Payment[] = [
  {
    id: "PAY-08-2026",
    invoiceId: "INV-08-2026",
    title: "Hóa đơn tháng 08/2026",
    paidAt: "03/09/2026",
    amount: "4.380.000đ",
    method: "Chuyển khoản ngân hàng",
    status: "Thành công",
  },
]

export const maintenanceRequests: MaintenanceRequest[] = [
  {
    id: "#MR00123",
    roomCode: "A203",
    category: "Máy lạnh",
    relatedEquipment: "Máy lạnh",
    title: "Máy lạnh không hoạt động",
    description:
      "Máy lạnh trong phòng không bật được từ tối qua, đèn nguồn không sáng.",
    priority: "Quan trọng",
    status: "ĐANG XỬ LÝ",
    createdAt: "15/09/2026",
    timeline: [
      { label: "Đã gửi", date: "15/09/2026", done: true },
      { label: "Đã tiếp nhận", date: "15/09/2026", done: true },
      { label: "Đang xử lý", date: "16/09/2026", done: true },
      { label: "Hoàn thành", done: false },
    ],
  },
  {
    id: "#MR00122",
    roomCode: "A203",
    category: "Nước",
    relatedEquipment: "Vòi nước",
    title: "Vòi nước bị rò rỉ",
    description: "Vòi nước khu vực bếp đã được kiểm tra và thay gioăng mới.",
    priority: "Bình thường",
    status: "HOÀN THÀNH",
    createdAt: "10/09/2026",
    timeline: [
      { label: "Đã gửi", date: "10/09/2026", done: true },
      { label: "Đã tiếp nhận", date: "10/09/2026", done: true },
      { label: "Đang xử lý", date: "11/09/2026", done: true },
      { label: "Hoàn thành", date: "12/09/2026", done: true },
    ],
  },
]

export const bookings: Booking[] = [
  {
    id: "BK-2026-0018",
    propertyName: "Sunrise Residence",
    roomCode: "A203",
    viewingDate: "25/07/2026 · 10:00",
    depositAmount: "7.600.000đ",
    paymentStatus: "Đã thanh toán",
    bookingStatus: "Converted to Rental",
  },
  {
    id: "BK-2026-0012",
    propertyName: "Nhà trọ Mộc Miên",
    roomCode: "B204",
    viewingDate: "20/07/2026 · 14:30",
    depositAmount: "0đ",
    paymentStatus: "Hoàn tiền",
    bookingStatus: "Cancelled",
  },
]

export const notifications: Notification[] = [
  {
    id: "notification-bill",
    title: "Hóa đơn tháng 9 sắp đến hạn",
    body: "Hạn thanh toán của hóa đơn INV-09-2026 là 05/10/2026.",
    date: "Hôm nay",
    read: false,
    href: "/my-bills",
  },
  {
    id: "notification-contract",
    title: "Hợp đồng của bạn còn 30 ngày",
    body: "Bạn có thể yêu cầu gia hạn hợp đồng ngay trên TrọViệt.",
    date: "Hôm qua",
    read: false,
    href: "/my-contracts",
  },
  {
    id: "notification-maintenance",
    title: "Yêu cầu sửa chữa #MR00123 đang được xử lý",
    body: "Kỹ thuật viên đã tiếp nhận yêu cầu máy lạnh của bạn.",
    date: "16/09/2026",
    read: true,
    href: "/my-room",
  },
  {
    id: "notification-completed",
    title: "Yêu cầu sửa chữa đã hoàn thành",
    body: "Vòi nước phòng A203 đã được xử lý xong.",
    date: "12/09/2026",
    read: true,
    href: "/my-room",
  },
]

export const profile: Profile = {
  fullName: "Ngọc Anh",
  email: "ngocanh@example.com",
  phone: "0909 123 456",
}
