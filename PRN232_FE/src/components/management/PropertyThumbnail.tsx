import { useState } from "react"

export default function PropertyThumbnail({ name, imageUrl }: { name: string; imageUrl?: string | null }) {
  const [failedUrl, setFailedUrl] = useState<string | null>(null)
  const source = imageUrl?.trim()
  return (
    <div className="grid size-16 shrink-0 place-items-center overflow-hidden rounded-lg bg-slate-100">
      {source && source !== failedUrl ? (
        <img
          src={source}
          alt={`Ảnh ${name}`}
          loading="lazy"
          className="size-full object-cover"
          onError={() => setFailedUrl(source)}
        />
      ) : (
        <span className="px-1 text-center text-xs text-slate-500">Chưa có ảnh</span>
      )}
    </div>
  )
}
