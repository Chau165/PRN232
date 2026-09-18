import { useState } from "react"
import LoginRequiredModal from "@/components/auth/LoginRequiredModal"
import Footer from "@/components/common/Footer"
import Navbar from "@/components/common/Navbar"
import AvailableRooms from "@/components/room/AvailableRooms"
import RoomDetailModal from "@/components/room/RoomDetailModal"
import { routes } from "@/constants/routes"
import { rooms } from "@/data/rooms"
import type { Room } from "@/types/room"

export default function AvailableRoomsPage() {
  const [selectedRoom, setSelectedRoom] = useState<Room | null>(null)
  const [loginRequired, setLoginRequired] = useState(false)

  return (
    <main className="min-h-screen bg-[#fcfdfd] text-slate-800">
      <Navbar />
      <div className="mx-auto max-w-[1180px] px-6 pb-20 pt-7">
        <nav className="flex items-center gap-2 text-[13px] font-medium text-slate-400">
          <a href={routes.home} className="hover:text-[#087775]">
            Trang chủ
          </a>
          <span>›</span>
          <a href={routes.propertyDetail} className="hover:text-[#087775]">
            Sunrise Residence
          </a>
          <span>›</span>
          <span className="text-slate-600">Phòng đang trống</span>
        </nav>
        <AvailableRooms
          rooms={rooms}
          onOpenRoom={setSelectedRoom}
          onLoginRequired={() => setLoginRequired(true)}
        />
      </div>
      {selectedRoom && (
        <RoomDetailModal
          room={selectedRoom}
          onClose={() => setSelectedRoom(null)}
          onLoginRequired={() => setLoginRequired(true)}
        />
      )}
      {loginRequired && (
        <LoginRequiredModal onClose={() => setLoginRequired(false)} />
      )}
      <Footer />
    </main>
  )
}
