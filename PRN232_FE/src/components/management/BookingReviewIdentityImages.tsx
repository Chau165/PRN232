import { useEffect, useState } from "react"
import { readReviewIdentity, type ApplicationPerson, type BookingApplication } from "@/services/bookingApplicationService"

type Side = "Front" | "Back"
type ImageState = { url?: string; failed?: boolean }

export default function BookingReviewIdentityImages({ token, application, person }: {
  token: string; application: BookingApplication; person: ApplicationPerson
}) {
  const [images, setImages] = useState<Partial<Record<Side, ImageState>>>({})
  const [attempt, setAttempt] = useState(0)
  const [expired, setExpired] = useState(false)
  const applicationId = application.bookingApplicationId
  const version = application.version
  const personId = person.personId
  const { hasFront, hasBack } = person

  useEffect(() => {
    const controller = new AbortController()
    setImages({}); setExpired(false)
    async function load(side: Side) {
      try {
        const result = await readReviewIdentity(token, { bookingApplicationId: applicationId }, personId, side, controller.signal)
        const url = new URL(result.url)
        if (url.protocol !== "https:" || url.username || url.password) throw Error("Invalid image URL")
        if (!controller.signal.aborted) setImages(current => ({ ...current, [side]: { url: url.href } }))
      } catch {
        if (!controller.signal.aborted) setImages(current => ({ ...current, [side]: { failed: true } }))
      }
    }
    // Fetch both sides independently so one unavailable image cannot hide the other.
    if (hasFront) void load("Front")
    if (hasBack) void load("Back")
    const timer = setTimeout(() => { controller.abort(); setImages({}); setExpired(true) }, 110000)
    return () => { controller.abort(); clearTimeout(timer) }
  }, [token, applicationId, version, personId, person.revision, hasFront, hasBack, attempt])

  return <div className="grid gap-4 pt-2 sm:grid-cols-2">
    {(["Front", "Back"] as const).map(side => {
      const present = side === "Front" ? hasFront : hasBack
      const image = images[side]
      const title = side === "Front" ? "Mặt trước" : "Mặt sau"
      return <figure key={side} className="min-w-0 space-y-2">
        <figcaption className="text-sm font-semibold text-slate-700">{title}</figcaption>
        <div className="flex min-h-40 items-center justify-center rounded-xl border border-slate-200 bg-white p-3">
          {!present ? <p className="text-sm text-slate-500">Chưa có ảnh {title.toLowerCase()}.</p>
            : expired ? <p className="text-sm text-slate-600">Ảnh đã hết thời gian xem.</p>
            : image?.url ? <img src={image.url} alt={`CCCD ${title.toLowerCase()} · ${person.fullName}`} referrerPolicy="no-referrer" className="max-h-80 w-full object-contain" onError={() => setImages(current => ({ ...current, [side]: { failed: true } }))}/>
            : image?.failed ? <p role="alert" className="text-sm text-red-700">Không tải được ảnh {title.toLowerCase()}. Hãy kiểm tra quyền và thử lại.</p>
            : <p role="status" className="text-sm text-slate-500">Đang tải ảnh {title.toLowerCase()}…</p>}
        </div>
        {present && (expired || image?.failed) && <button type="button" className="text-sm font-semibold text-teal-700 underline" onClick={() => setAttempt(value => value + 1)}>Tải lại ảnh {title.toLowerCase()}</button>}
      </figure>
    })}
  </div>
}
