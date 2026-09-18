import { useState } from "react"
import Icon from "@/components/common/Icon"
import { nearbyPlaces } from "@/data/nearbyPlaces"

const categories = [
  "Tất cả",
  "Trường học",
  "Mua sắm",
  "Ăn uống",
  "Y tế",
  "Giao thông",
]

const convenienceNotes = [
  "800m đến Đại học Sư phạm Kỹ thuật TP.HCM",
  "200m đến trạm xe buýt",
  "350m đến cửa hàng tiện lợi",
  "1,5km đến Vincom",
  "Gần Võ Văn Ngân và Phạm Văn Đồng",
]

export default function NeighborhoodMap() {
  const [selected, setSelected] = useState("Sunrise Residence")
  const [category, setCategory] = useState("Tất cả")
  const activePlace = nearbyPlaces.find((place) => place.name === selected)
  const shownPlaces =
    category === "Tất cả"
      ? nearbyPlaces
      : nearbyPlaces.filter((place) => place.category === category)

  return (
    <section className="mt-20 border-t border-slate-100 pt-12">
      <div className="mb-6">
        <h2 className="font-display text-[27px] font-bold tracking-[-.04em] text-slate-900">
          Vị trí & khu vực xung quanh
        </h2>
        <p className="mt-1 max-w-[680px] text-[13px] leading-6 text-slate-500">
          Khám phá vị trí khu trọ, các tuyến đường và tiện ích xung quanh trước
          khi quyết định thuê.
        </p>
      </div>
      <div className="neighborhood-map relative h-[570px] overflow-hidden rounded-3xl border border-slate-100 bg-[#e8eff0] shadow-[0_6px_26px_rgba(16,65,67,.09)]">
        <svg
          className="absolute inset-0 h-full w-full"
          viewBox="0 0 1000 570"
          preserveAspectRatio="none"
          aria-label="Bản đồ khu vực Thủ Đức"
        >
          <rect width="1000" height="570" fill="#e8eff0" />
          <path
            d="M-30 430 C160 350 230 470 400 370 S690 260 1040 350"
            fill="none"
            stroke="#fff"
            strokeWidth="25"
          />
          <path
            d="M75 -10 C180 125 290 160 430 215 S720 245 1010 120"
            fill="none"
            stroke="#fff"
            strokeWidth="19"
          />
          <path
            d="M400 -10 C430 150 540 255 510 580"
            fill="none"
            stroke="#fff"
            strokeWidth="18"
          />
          <path
            d="M-10 170 C160 210 260 260 385 350 S730 450 1020 480"
            fill="none"
            stroke="#fff"
            strokeWidth="14"
          />
          <path
            d="M700 -10 C650 130 755 215 690 360 S710 510 840 590"
            fill="none"
            stroke="#fff"
            strokeWidth="13"
          />
          <path
            d="M-30 430 C160 350 230 470 400 370 S690 260 1040 350"
            fill="none"
            stroke="#cbd8dc"
            strokeWidth="2"
          />
          <path
            d="M75 -10 C180 125 290 160 430 215 S720 245 1010 120"
            fill="none"
            stroke="#cbd8dc"
            strokeWidth="2"
          />
          <path
            d="M400 -10 C430 150 540 255 510 580"
            fill="none"
            stroke="#cbd8dc"
            strokeWidth="2"
          />
          <path
            d="M-10 170 C160 210 260 260 385 350 S730 450 1020 480"
            fill="none"
            stroke="#cbd8dc"
            strokeWidth="2"
          />
          <path
            d="M700 -10 C650 130 755 215 690 360 S710 510 840 590"
            fill="none"
            stroke="#cbd8dc"
            strokeWidth="2"
          />
          <path
            d="M120 285 C230 290 330 305 440 290"
            fill="none"
            stroke="#fefefe"
            strokeWidth="10"
          />
          <path
            d="M570 80 C590 170 640 220 790 240"
            fill="none"
            stroke="#fefefe"
            strokeWidth="9"
          />
          <text x="248" y="161" fill="#667b81" fontSize="14" fontWeight="600">
            Võ Văn Ngân
          </text>
          <text x="555" y="212" fill="#667b81" fontSize="13" fontWeight="600">
            Đặng Văn Bi
          </text>
          <text x="438" y="91" fill="#667b81" fontSize="13" fontWeight="600">
            Lê Văn Chí
          </text>
          <text x="707" y="432" fill="#667b81" fontSize="13" fontWeight="600">
            Kha Vạn Cân
          </text>
          <text x="118" y="390" fill="#667b81" fontSize="14" fontWeight="600">
            Phạm Văn Đồng
          </text>
        </svg>
        <div className="absolute left-[46%] top-[44%] z-20 -translate-x-1/2 -translate-y-1/2">
          <button
            onClick={() => setSelected("Sunrise Residence")}
            className="group relative grid h-12 w-12 place-items-center rounded-full border-4 border-white bg-[#087775] text-white shadow-[0_5px_15px_rgba(8,119,117,.35)]"
          >
            <Icon name="building" size={20} />
            <span className="absolute -bottom-6 whitespace-nowrap rounded-md bg-[#087775] px-2 py-1 text-[10px] font-bold">
              Sunrise Residence
            </span>
          </button>
        </div>
        {shownPlaces.map((place) => (
          <button
            key={place.name}
            onClick={() => setSelected(place.name)}
            style={{ left: place.x, top: place.y }}
            className={`absolute z-10 -translate-x-1/2 -translate-y-1/2 rounded-full border-2 border-white p-1.5 shadow-md ${
              selected === place.name
                ? "bg-[#087775] text-white ring-4 ring-teal-100"
                : "bg-white text-slate-600"
            }`}
            aria-label={place.name}
          >
            <Icon name="pin" size={15} />
          </button>
        ))}
        <div className="absolute right-5 top-5 z-20 overflow-hidden rounded-xl bg-white shadow-md">
          <button className="grid h-10 w-10 place-items-center border-b border-slate-100 text-xl text-slate-700">
            +
          </button>
          <button className="grid h-10 w-10 place-items-center text-xl text-slate-700">
            −
          </button>
        </div>
        <div className="absolute bottom-5 right-5 z-20 flex gap-2">
          <button
            className="grid h-10 w-10 place-items-center rounded-xl bg-white text-slate-600 shadow-md"
            aria-label="Vị trí hiện tại"
          >
            <Icon name="gps" size={18} />
          </button>
          <button
            className="grid h-10 w-10 place-items-center rounded-xl bg-white text-slate-600 shadow-md"
            aria-label="Toàn màn hình"
          >
            ↗
          </button>
          <button className="flex items-center gap-1.5 rounded-xl bg-white px-3 text-[11px] font-bold text-[#087775] shadow-md">
            <Icon name="arrow" size={15} />
            Chỉ đường
          </button>
        </div>
        <div className="absolute bottom-5 left-5 z-30 w-[290px] rounded-2xl bg-white p-4 shadow-[0_14px_30px_rgba(26,59,61,.18)]">
          {selected === "Sunrise Residence" ? (
            <>
              <p className="text-[14px] font-bold text-slate-800">
                Sunrise Residence
              </p>
              <p className="mt-1 text-[11px] text-slate-500">
                Đường Võ Văn Ngân, Thủ Đức
              </p>
              <p className="mt-3 text-[12px] font-medium text-slate-600">
                Cách bạn 1,2 km
              </p>
              <p className="mt-1 text-[12px] font-bold text-[#087775]">
                Từ 3.500.000đ/tháng · 5 phòng trống
              </p>
              <button className="mt-3 text-[11px] font-bold text-[#087775]">
                Xem chỉ đường <span>→</span>
              </button>
            </>
          ) : (
            activePlace && (
              <>
                <p className="text-[14px] font-bold text-slate-800">
                  {activePlace.name}
                </p>
                <p className="mt-1 text-[11px] text-[#087775]">
                  {activePlace.category}
                </p>
                <p className="mt-3 text-[12px] font-medium text-slate-600">
                  {activePlace.distance} · {activePlace.travel}
                </p>
              </>
            )
          )}
        </div>
      </div>
      <div className="mt-8 grid grid-cols-[1.2fr_.8fr] gap-12">
        <div>
          <h3 className="font-display text-[20px] font-bold tracking-[-.03em] text-slate-900">
            Tiện ích xung quanh
          </h3>
          <div className="mt-4 flex flex-wrap gap-2">
            {categories.map((item) => (
              <button
                key={item}
                onClick={() => setCategory(item)}
                className={`rounded-full px-3 py-1.5 text-[11px] font-semibold ${
                  category === item
                    ? "bg-[#087775] text-white"
                    : "bg-slate-100 text-slate-500 hover:bg-teal-50 hover:text-[#087775]"
                }`}
              >
                {item}
              </button>
            ))}
          </div>
          <div className="mt-5 space-y-1">
            {shownPlaces.slice(0, 3).map((place) => (
              <button
                key={place.name}
                onClick={() => setSelected(place.name)}
                className="flex w-full items-center justify-between rounded-xl px-3 py-3 text-left hover:bg-slate-50"
              >
                <span>
                  <span className="block text-[13px] font-semibold text-slate-700">
                    {place.name}
                  </span>
                  <span className="mt-0.5 block text-[11px] text-slate-500">
                    {place.distance} · {place.travel}
                  </span>
                </span>
                <span className="text-[#087775]">›</span>
              </button>
            ))}
          </div>
        </div>
        <div className="rounded-2xl bg-[#f1f8f7] p-6">
          <p className="text-[11px] font-bold uppercase tracking-[.14em] text-[#087775]">
            Vị trí thuận tiện
          </p>
          <ul className="mt-4 space-y-2.5 text-[12px] leading-5 text-slate-600">
            {convenienceNotes.map((item) => (
              <li key={item} className="flex gap-2">
                <span className="mt-1.5 h-1.5 w-1.5 shrink-0 rounded-full bg-[#087775]" />
                {item}
              </li>
            ))}
          </ul>
        </div>
      </div>
    </section>
  )
}
