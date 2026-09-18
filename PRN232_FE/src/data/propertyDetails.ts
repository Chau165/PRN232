import type { IconName } from "@/components/common/Icon"
import { properties } from "@/data/properties"

export const sunrisePhotos = [
  properties[0].image,
  "https://images.unsplash.com/photo-1600210492486-724fe5c67fb0?auto=format&fit=crop&w=900&q=85",
  "https://images.unsplash.com/photo-1600566753086-00f18fb6b3ea?auto=format&fit=crop&w=900&q=85",
  "https://images.unsplash.com/photo-1615874694520-474822394e73?auto=format&fit=crop&w=900&q=85",
  "https://images.unsplash.com/photo-1616486338812-3dadae4b4ace?auto=format&fit=crop&w=900&q=85",
]

export const detailAmenities: Array<[IconName, string]> = [
  ["wifi", "Wi-Fi"],
  ["snow", "Máy lạnh"],
  ["car", "Bãi giữ xe"],
  ["camera", "Camera"],
  ["toilet", "WC riêng"],
  ["washer", "Máy giặt"],
  ["lift", "Thang máy"],
  ["lock", "Khóa cửa an toàn"],
]

export const propertyCosts = [
  ["Điện", "3.500đ/kWh"],
  ["Nước", "100.000đ/người/tháng"],
  ["Internet", "100.000đ/phòng/tháng"],
  ["Phí dịch vụ", "50.000đ/tháng"],
  ["Bãi xe", "100.000đ/xe/tháng"],
]

export const propertyRules = [
  "Giữ vệ sinh chung",
  "Không gây tiếng ồn sau 22:00",
  "Khách ở thêm cần đăng ký",
  "Tuân thủ quy định an toàn",
]
