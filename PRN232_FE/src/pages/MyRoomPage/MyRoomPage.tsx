import { useState } from "react"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import CustomerRoomDrawer from "@/components/customer/CustomerRoomDrawer"
import MaintenanceDetailDrawer from "@/components/customer/MaintenanceDetailDrawer"
import ReportDamageModal from "@/components/customer/ReportDamageModal"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import Icon from "@/components/common/Icon"
import {
  currentRoom,
  maintenanceRequests as initialRequests,
} from "@/data/customer"
import type {
  MaintenanceRequest,
  MaintenanceRequestForm,
} from "@/types/customer"

export default function MyRoomPage() {
  const [activeTab, setActiveTab] =
    useState<"current" | "history" | "maintenance">("current")
  const [roomOpen, setRoomOpen] = useState(false)
  const [reportOpen, setReportOpen] = useState(false)
  const [selectedRequest, setSelectedRequest] =
    useState<MaintenanceRequest | null>(null)
  const [requests, setRequests] = useState(initialRequests)
  const [toast, setToast] = useState("")

  function submitReport(form: MaintenanceRequestForm) {
    const request: MaintenanceRequest = {
      id: `#MR00${124 + requests.length}`,
      roomCode: currentRoom.roomCode,
      category: form.category,
      relatedEquipment: form.relatedEquipment,
      title: form.title,
      description: form.description,
      priority: form.priority,
      status: "ĐÃ GỬI",
      createdAt: "16/09/2026",
      imageName: form.imageName,
      timeline: [
        { label: "Đã gửi", date: "16/09/2026", done: true },
        { label: "Đã tiếp nhận", done: false },
        { label: "Đang xử lý", done: false },
        { label: "Hoàn thành", done: false },
      ],
    }
    setRequests((items) => [request, ...items])
    setReportOpen(false)
    setActiveTab("maintenance")
    setToast("Báo cáo hư hỏng đã được gửi")
  }

  function confirmRepair(id: string, rating?: number) {
    setRequests((items) =>
      items.map((item) => (item.id === id ? { ...item, rating } : item)),
    )
    setSelectedRequest(null)
    setToast(
      rating
        ? `Đã xác nhận hoàn thành và đánh giá ${rating} sao`
        : "Đã xác nhận yêu cầu sửa chữa hoàn thành",
    )
  }

  return (
    <CustomerPageShell
      title="Phòng của tôi"
      description="Quản lý phòng đang thuê, hợp đồng và các yêu cầu hỗ trợ của bạn."
    >
      <div className="flex flex-wrap gap-2 border-b border-slate-100">
        {[
          ["current", "Đang thuê"],
          ["history", "Lịch sử thuê"],
          ["maintenance", "Yêu cầu sửa chữa"],
        ].map(([value, label]) => (
          <button
            key={value}
            onClick={() => setActiveTab(value as typeof activeTab)}
            className={`border-b-2 px-3 pb-3 text-[14px] font-semibold ${
              activeTab === value
                ? "border-[#087775] text-[#087775]"
                : "border-transparent text-slate-400 hover:text-slate-700"
            }`}
          >
            {label}
          </button>
        ))}
      </div>
      {activeTab === "current" && (
        <section className="mt-7 rounded-2xl bg-white p-6 shadow-[0_5px_24px_rgba(16,65,67,.08)]">
          <div className="flex flex-col gap-6 lg:flex-row lg:items-start">
            <img
              src={currentRoom.images[0]}
              alt={`Phòng ${currentRoom.roomCode}`}
              className="h-52 w-full rounded-xl object-cover lg:w-64"
            />
            <div className="min-w-0 flex-1">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">
                    {currentRoom.propertyName}
                  </p>
                  <h2 className="mt-2 font-display text-[24px] font-bold text-slate-900">
                    Phòng {currentRoom.roomCode}
                  </h2>
                  <p className="mt-1 flex items-center gap-1.5 text-[14px] text-slate-500">
                    <Icon name="pin" size={15} />
                    {currentRoom.propertyAddress}
                  </p>
                </div>
                <StatusBadge tone="green">ĐANG THUÊ</StatusBadge>
              </div>
              <div className="mt-5 grid gap-4 text-[14px] sm:grid-cols-2">
                <div>
                  <p className="text-slate-400">Giá thuê</p>
                  <p className="mt-1 font-bold text-[#087775]">
                    {currentRoom.monthlyRent} / tháng
                  </p>
                </div>
                <div>
                  <p className="text-slate-400">Hợp đồng</p>
                  <p className="mt-1 font-semibold text-slate-700">
                    {currentRoom.contractStart} - {currentRoom.contractEnd}
                  </p>
                </div>
              </div>
              <div className="mt-5 flex flex-wrap gap-2">
                {currentRoom.equipment.map((item) => (
                  <span key={item} className="room-amenity">
                    {item}
                  </span>
                ))}
              </div>
              <div className="mt-6 flex flex-wrap gap-3">
                <button
                  onClick={() => setRoomOpen(true)}
                  className="rounded-lg bg-[#087775] px-4 py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
                >
                  Xem chi tiết
                </button>
                <button
                  onClick={() => setReportOpen(true)}
                  className="rounded-lg bg-teal-50 px-4 py-3 text-[13px] font-bold text-[#087775] hover:bg-teal-100"
                >
                  Báo hư hỏng
                </button>
              </div>
            </div>
          </div>
        </section>
      )}
      {activeTab === "history" && (
        <section className="mt-7 rounded-2xl border border-dashed border-slate-200 bg-white p-10 text-center">
          <p className="font-display text-[20px] font-bold text-slate-800">
            Lịch sử thuê
          </p>
          <p className="mt-2 text-[14px] text-slate-500">
            Các hợp đồng đã kết thúc sẽ hiển thị tại đây.
          </p>
        </section>
      )}
      {activeTab === "maintenance" && (
        <section className="mt-7 space-y-4">
          {requests.map((request) => (
            <button
              key={request.id}
              onClick={() => setSelectedRequest(request)}
              className="w-full rounded-2xl bg-white p-5 text-left shadow-[0_3px_18px_rgba(16,65,67,.07)] transition hover:-translate-y-0.5 hover:shadow-[0_8px_28px_rgba(15,54,60,.1)]"
            >
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <p className="text-[12px] font-bold uppercase tracking-[.12em] text-[#087775]">
                    {request.id} · Phòng {request.roomCode}
                  </p>
                  <h3 className="mt-2 text-[16px] font-bold text-slate-800">
                    {request.title}
                  </h3>
                  <p className="mt-1 text-[13px] text-slate-500">
                    {request.createdAt} · {request.category}
                  </p>
                </div>
                <StatusBadge
                  tone={request.status === "HOÀN THÀNH" ? "green" : "amber"}
                >
                  {request.status}
                </StatusBadge>
              </div>
              <div className="mt-5 flex flex-wrap items-center gap-2 sm:gap-0">
                {request.timeline.map((item, index) => (
                  <div key={item.label} className="flex items-center gap-2">
                    <span
                      className={`grid h-6 w-6 place-items-center rounded-full text-[10px] font-bold ${
                        item.done
                          ? "bg-[#087775] text-white"
                          : "bg-slate-100 text-slate-400"
                      }`}
                    >
                      {item.done ? "✓" : index + 1}
                    </span>
                    <span
                      className={`text-[11px] font-semibold ${
                        item.done ? "text-slate-600" : "text-slate-400"
                      }`}
                    >
                      {item.label}
                    </span>
                    {index < request.timeline.length - 1 && (
                      <span className="mx-1 hidden h-px w-5 bg-slate-200 sm:block" />
                    )}
                  </div>
                ))}
              </div>
            </button>
          ))}
        </section>
      )}
      {roomOpen && (
        <CustomerRoomDrawer
          room={currentRoom}
          onClose={() => setRoomOpen(false)}
        />
      )}
      {reportOpen && (
        <ReportDamageModal
          roomCode={currentRoom.roomCode}
          equipment={currentRoom.equipment}
          onClose={() => setReportOpen(false)}
          onSubmit={submitReport}
        />
      )}
      {selectedRequest && (
        <MaintenanceDetailDrawer
          request={selectedRequest}
          onClose={() => setSelectedRequest(null)}
          onConfirm={
            selectedRequest.status === "HOÀN THÀNH" ? confirmRepair : undefined
          }
        />
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </CustomerPageShell>
  )
}
