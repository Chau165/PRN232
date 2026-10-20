export default function MapSection() {
  return (
    <section id="khu-vuc" className="scroll-mt-24">
      <div className="mx-auto max-w-[1180px] px-6 pb-20 pt-4 lg:pt-8">
        <div className="mb-6">
          <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
            Khám phá khu vực
          </p>
          <h2 className="mt-1 font-display text-[27px] font-bold tracking-[-.04em] text-slate-900">
            Khu Vực
          </h2>
          <p className="mt-1 text-[13px] text-slate-500">
            Bản đồ chưa khả dụng cho dữ liệu công khai hiện tại.
          </p>
        </div>
        <div className="grid min-h-32 place-items-center rounded-2xl border border-dashed border-slate-300 bg-slate-50 px-6 text-center text-[13px] text-slate-500">
          Vị trí chính xác và bản đồ không được cung cấp bởi API public.
        </div>
      </div>
    </section>
  )
}
