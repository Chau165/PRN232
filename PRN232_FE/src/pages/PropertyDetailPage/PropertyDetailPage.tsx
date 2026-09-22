import { useState } from "react"
import Footer from "@/components/common/Footer"
import Icon from "@/components/common/Icon"
import Navbar from "@/components/common/Navbar"
import DetailSection from "@/components/property/DetailSection"
import NeighborhoodMap from "@/components/property/NeighborhoodMap"
import PropertyGallery from "@/components/property/PropertyGallery"
import LoginRequiredModal from "@/components/auth/LoginRequiredModal"
import { properties } from "@/data/properties"
import {
  detailAmenities,
  propertyCosts,
  propertyRules,
  sunrisePhotos,
} from "@/data/propertyDetails"
import { routes } from "@/constants/routes"
import ViewingScheduleModal from "@/components/room/ViewingScheduleModal"

export default function PropertyDetailPage() {
  const [galleryOpen, setGalleryOpen] = useState(false)
  const [loginRequired, setLoginRequired] = useState(false)
  const [scheduleOpen, setScheduleOpen] = useState(false)
  const property = properties[0]

  return (
    <main className="min-h-screen bg-[#fcfdfd] text-slate-800">
      <Navbar />
      <div className="mx-auto max-w-[1180px] px-6 pb-20 pt-7">
        <nav className="flex items-center gap-2 text-[13px] font-medium text-slate-400">
          <a href={routes.home} className="hover:text-[#087775]">
            Trang chủ
          </a>
          <span>›</span>
          <span>Thủ Đức</span>
          <span>›</span>
          <span className="text-slate-600">{property.name}</span>
        </nav>
        <div className="mt-8 grid grid-cols-1 items-start gap-8 lg:grid-cols-[minmax(0,1fr)_312px] lg:gap-20">
          <div>
            <p className="mb-2 text-[12px] font-bold uppercase tracking-[.16em] text-[#087775]">
              Khu phòng trọ · Thủ Đức
            </p>
            <h1 className="font-display text-[37px] font-bold tracking-[-.05em] text-slate-900">
              {property.name}
            </h1>
            <p className="mt-2 flex items-center gap-1.5 text-[15px] text-slate-500">
              <Icon name="pin" size={16} />
              {property.address}
            </p>
            <div className="mt-4 flex items-center gap-3 text-[13px] font-medium">
              <span className="rounded-full bg-teal-50 px-3 py-1.5 text-[#087775]">
                Cách bạn {property.distance}
              </span>
              <span className="rounded-full bg-emerald-50 px-3 py-1.5 text-emerald-700">
                {property.rooms} phòng đang trống
              </span>
            </div>
          </div>
          <div className="pt-1">
            <p className="text-[13px] font-medium text-slate-500">Giá từ</p>
            <p className="mt-1 whitespace-nowrap text-[25px] font-bold tracking-[-.04em] text-[#087775]">
              {property.price}
              <span className="ml-1 text-[14px] font-normal tracking-normal text-slate-400">
                / tháng
              </span>
            </p>
            <div className="mt-5 grid grid-cols-2 gap-2">
              <a
                href={routes.availableRooms}
                className="rounded-lg bg-[#087775] px-2 py-3 text-center text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
              >
                Xem phòng trống
              </a>
              <button
                onClick={() => {
                  if (window.localStorage.getItem("troviet-auth") === "true") setScheduleOpen(true)
                  else setLoginRequired(true)
                }}
                className="rounded-lg bg-teal-50 px-2 py-3 text-[13px] font-bold text-[#087775] hover:bg-teal-100"
              >
                Đặt lịch xem
              </button>
            </div>
          </div>
        </div>
        <PropertyGallery
          photos={sunrisePhotos}
          onOpen={() => setGalleryOpen(true)}
        />
        <div className="mt-12">
          <DetailSection title="Giới thiệu">
            <p className="mt-4 max-w-[680px] text-[15px] leading-7 text-slate-600">
              Sunrise Residence là khu phòng trọ hiện đại nằm gần trung tâm Thủ
              Đức, phù hợp cho sinh viên và người đi làm. Không gian được vận
              hành chỉn chu, an ninh và tiện nghi để bạn an tâm tận hưởng nhịp
              sống riêng.
            </p>
          </DetailSection>
          <DetailSection title="Tiện ích">
            <div className="mt-5 grid grid-cols-4 gap-y-5">
              {detailAmenities.map(([icon, label]) => (
                <div
                  key={label}
                  className="flex items-center gap-2.5 text-[14px] font-medium text-slate-600"
                >
                  <span className="grid h-8 w-8 place-items-center rounded-lg bg-[#f1f8f7] text-[#087775]">
                    <Icon name={icon} size={16} />
                  </span>
                  {label}
                </div>
              ))}
            </div>
          </DetailSection>
          <DetailSection title="Chi phí sinh hoạt">
            <div className="mt-4 overflow-hidden rounded-xl bg-slate-50/70 px-5">
              {propertyCosts.map(([label, value]) => (
                <div
                  key={label}
                  className="flex justify-between border-b border-slate-100 py-3.5 text-[14px] last:border-0"
                >
                  <span className="text-slate-500">{label}</span>
                  <strong className="font-semibold text-slate-700">
                    {value}
                  </strong>
                </div>
              ))}
            </div>
          </DetailSection>
          <DetailSection title="Nội quy">
            <ul className="mt-4 space-y-3">
              {propertyRules.map((rule) => (
                <li
                  key={rule}
                  className="flex items-center gap-3 text-[14px] text-slate-600"
                >
                  <span className="grid h-5 w-5 place-items-center rounded-full bg-teal-50 text-[#087775]">
                    <Icon name="check" size={12} />
                  </span>
                  {rule}
                </li>
              ))}
            </ul>
          </DetailSection>
        </div>
        <NeighborhoodMap />
      </div>
      {galleryOpen && (
        <div className="fixed inset-0 z-50 grid place-items-center bg-slate-950/60 p-8">
          <div className="relative max-h-full max-w-5xl rounded-2xl bg-white p-4">
            <button
              onClick={() => setGalleryOpen(false)}
              className="absolute right-6 top-5 z-10 grid h-9 w-9 place-items-center rounded-full bg-white text-xl shadow-md"
            >
              ×
            </button>
            <div className="grid max-h-[80vh] grid-cols-2 gap-3 overflow-auto">
              {sunrisePhotos.map((photo) => (
                <img
                  key={photo}
                  src={photo}
                  alt="Không gian Sunrise Residence"
                  className="rounded-xl"
                />
              ))}
            </div>
          </div>
        </div>
      )}
      {loginRequired && (
        <LoginRequiredModal onClose={() => setLoginRequired(false)} />
      )}
      {scheduleOpen && <ViewingScheduleModal onClose={() => setScheduleOpen(false)} />}
      <Footer />
    </main>
  )
}
