export default function HeroSearch() {
  return (
    <section id="gioi-thieu" className="hero-wash scroll-mt-24">
      <div className="relative mx-auto grid max-w-[1180px] items-center gap-8 px-6 py-10 lg:grid-cols-[.9fr_1.1fr] lg:gap-12 lg:py-12">
        <div className="relative z-10 max-w-xl">
          <p className="mb-4 text-[12px] font-bold uppercase tracking-[.2em] text-[#087775]">
            Không gian sống đúng ý bạn
          </p>
          <h1 className="font-display text-[40px] font-bold leading-[1.15] tracking-[-.05em] text-slate-900 sm:text-[48px]">
            Tìm phòng trọ phù hợp với bạn
          </h1>
          <p className="mt-5 max-w-lg text-[16px] leading-7 text-slate-500">
            Khám phá phòng trống theo khu vực, mức giá và nhu cầu sinh hoạt.
          </p>
        </div>
        <div className="relative">
          <div className="absolute -inset-5 rounded-[42%] bg-[#e8f5f2]/80 blur-2xl" />
          <div className="relative overflow-hidden rounded-[28px] bg-white p-2 shadow-[0_18px_42px_rgba(17,68,70,.14)]">
            <img
              src="/hero-room.png"
              alt="Phòng trọ sáng thoáng của TrọViệt"
              className="h-[300px] w-full rounded-[22px] object-cover sm:h-[390px]"
            />
          </div>
        </div>
      </div>
    </section>
  )
}
