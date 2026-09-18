import Icon from "@/components/common/Icon"

export default function Footer() {
  return (
    <footer className="mt-20 border-t border-slate-100 bg-white">
      <div className="mx-auto flex max-w-[1180px] items-center justify-between px-6 py-8">
        <div className="flex items-center gap-2 text-[14px] font-semibold text-slate-700">
          <span className="grid h-7 w-7 place-items-center rounded-lg bg-[#087775] text-white">
            <Icon name="building" size={15} />
          </span>
          TrọViệt
        </div>
        <p className="text-[13px] text-slate-400">
          Tìm nơi ở, bắt đầu cuộc sống mới.
        </p>
        <p className="text-[12px] text-slate-400">© 2026 TrọViệt</p>
      </div>
    </footer>
  )
}
