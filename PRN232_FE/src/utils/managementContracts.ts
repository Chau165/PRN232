import { managedBookings, managementRooms } from "@/data/management"
import { canAccessProperty } from "@/utils/managementAuth"
import type { ManagedContract } from "@/types/management"
import type { CurrentUser } from "@/types/user"

const storageKey = "troviet-created-contracts"

export function isContractDate(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const date = new Date(`${value}T00:00:00Z`)
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value
}

export function formatContractDate(value: string) {
  return value.split("-").reverse().join("/")
}

// Keep the same calendar day; clamp to the last day for shorter months.
export function addContractMonths(start: string, months: number) {
  if (!isContractDate(start)) return ""
  const date = new Date(`${start}T00:00:00Z`)
  const day = date.getUTCDate()
  date.setUTCDate(1)
  date.setUTCMonth(date.getUTCMonth() + months)
  const lastDay = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate()
  date.setUTCDate(Math.min(day, lastDay))
  return date.toISOString().slice(0, 10)
}

export function getCreatedContracts(): ManagedContract[] {
  const stored = window.localStorage.getItem(storageKey)
  if (!stored) return []
  const contracts: unknown = JSON.parse(stored)
  if (!Array.isArray(contracts) || contracts.some((item) =>
    !item || typeof item.id !== "string" || typeof item.bookingId !== "string" ||
    typeof item.propertyId !== "string" || typeof item.startDate !== "string" ||
    typeof item.endDate !== "string")) {
    throw new Error("Không thể đọc hợp đồng đã lưu. Vui lòng kiểm tra dữ liệu trình duyệt.")
  }
  return contracts as ManagedContract[]
}

export function createBookingContract(user: CurrentUser, bookingId: string, endDate: string) {
  const booking = managedBookings.find((item) => item.id === bookingId)
  if ((user.role !== "ADMIN" && user.role !== "MANAGER") || !booking || !canAccessProperty(user, booking.propertyId)) {
    throw new Error("Bạn không có quyền tạo hợp đồng cho booking này.")
  }
  const contracts = getCreatedContracts()
  if (booking.status !== "Confirmed" || contracts.some((item) => item.bookingId === bookingId)) {
    throw new Error("Chỉ có thể tạo một hợp đồng cho booking đã xác nhận.")
  }
  if (!booking.customerPhone || !booking.moveInDate || !isContractDate(booking.moveInDate)) {
    throw new Error("Booking chưa có đủ số điện thoại hoặc ngày nhận phòng.")
  }
  if (!isContractDate(endDate) || endDate <= booking.moveInDate) {
    throw new Error("Ngày kết thúc phải sau ngày nhận phòng.")
  }
  const room = managementRooms.find((item) => item.propertyId === booking.propertyId && item.code === booking.roomCode)
  if (!room) throw new Error("Không tìm thấy thông tin phòng của booking.")

  const contract: ManagedContract = {
    id: `HD-${crypto.randomUUID()}`,
    bookingId,
    tenantId: booking.customerId ?? `booking-${booking.id}`,
    tenantName: booking.customerName,
    tenantPhone: booking.customerPhone,
    companions: booking.companions?.map((item) => ({ ...item })) ?? [],
    propertyId: booking.propertyId,
    propertyName: booking.propertyName,
    roomCode: booking.roomCode,
    startDate: formatContractDate(booking.moveInDate),
    endDate: formatContractDate(endDate),
    monthlyRent: room.price,
    deposit: booking.deposit,
    status: "Đang hiệu lực",
  }
  // Derive the booking's converted status from this same record on every read.
  window.localStorage.setItem(storageKey, JSON.stringify([...contracts, contract]))
  return contract
}
