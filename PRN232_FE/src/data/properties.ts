import type { Property } from "@/types/property"

export const properties: Property[] = [
  {
    id: 1,
    name: "Sunrise Residence",
    address: "Đường Võ Văn Ngân, Thủ Đức, TP.HCM",
    distance: "1,2 km",
    price: "3.500.000đ",
    rooms: 5,
    image:
      "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=900&q=85",
    x: "56%",
    y: "40%",
    amenities: ["Wi-Fi", "Máy lạnh", "Bãi xe", "Camera"],
    description: "Studio sáng thoáng, đầy đủ nội thất cơ bản, dọn vào ở ngay.",
  },
  {
    id: 2,
    name: "Nhà trọ Mộc Miên",
    address: "Đường Lê Văn Chí, Thủ Đức, TP.HCM",
    distance: "1,8 km",
    price: "2.800.000đ",
    rooms: 3,
    image:
      "https://images.unsplash.com/photo-1600210492486-724fe5c67fb0?auto=format&fit=crop&w=900&q=85",
    x: "30%",
    y: "60%",
    amenities: ["Wi-Fi", "Giặt sấy", "Bãi xe"],
    description: "Không gian yên tĩnh, gần ĐH Sư phạm Kỹ thuật và chợ Thủ Đức.",
  },
  {
    id: 3,
    name: "The Nest Bình Thọ",
    address: "Đường Đặng Văn Bi, Thủ Đức, TP.HCM",
    distance: "2,3 km",
    price: "4.200.000đ",
    rooms: 2,
    image:
      "https://images.unsplash.com/photo-1600566753086-00f18fb6b3ea?auto=format&fit=crop&w=900&q=85",
    x: "75%",
    y: "61%",
    amenities: ["Wi-Fi", "Thang máy", "Camera"],
    description:
      "Căn hộ mini mới, an ninh 24/7, quản lý vận hành chuyên nghiệp.",
  },
]
