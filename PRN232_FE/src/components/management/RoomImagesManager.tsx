import { useEffect, useRef, useState, type FormEvent } from "react"
import {
  deleteRoomImage,
  getRoomImages,
  setPrimaryRoomImage,
  uploadRoomImage,
} from "@/services/roomService"
import type { RoomImageDto } from "@/types/managementRoom"

type Props = {
  accessToken: string
  roomId: number
  onImagesChanged: () => void
}

function errorMessage(cause: unknown) {
  return cause instanceof Error
    ? cause.message
    : "Không thể cập nhật ảnh phòng. Vui lòng thử lại."
}

export default function RoomImagesManager({
  accessToken,
  roomId,
  onImagesChanged,
}: Props) {
  const [images, setImages] = useState<RoomImageDto[]>([])
  const [loading, setLoading] = useState(true)
  const [busy, setBusy] = useState(false)
  const [error, setError] = useState("")
  const [success, setSuccess] = useState("")
  const [file, setFile] = useState<File | null>(null)
  const [preview, setPreview] = useState("")
  const [isPrimary, setIsPrimary] = useState(false)
  const [reload, setReload] = useState(0)
  const inputRef = useRef<HTMLInputElement>(null)

  useEffect(() => {
    const controller = new AbortController()
    setLoading(true)
    getRoomImages(accessToken, roomId, controller.signal)
      .then((rows) => {
        if (!controller.signal.aborted) setImages(rows)
      })
      .catch((cause: unknown) => {
        if (!controller.signal.aborted) setError(errorMessage(cause))
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, roomId, reload])

  useEffect(() => {
    if (!file) {
      setPreview("")
      return
    }
    const url = URL.createObjectURL(file)
    setPreview(url)
    return () => URL.revokeObjectURL(url)
  }, [file])

  function selectFile(selected: File | undefined) {
    setError("")
    setSuccess("")
    setFile(null)
    if (!selected) return
    const validExtension = /\.(jpe?g|png|webp)$/i.test(selected.name)
    if (
      !validExtension ||
      !["image/jpeg", "image/png", "image/webp"].includes(selected.type)
    ) {
      setError("Vui lòng chọn ảnh JPEG, PNG hoặc WebP.")
    } else if (selected.size === 0 || selected.size > 5 * 1024 * 1024) {
      setError("Ảnh phải có dung lượng từ 1 byte đến 5 MiB.")
    } else {
      setFile(selected)
      return
    }
    if (inputRef.current) inputRef.current.value = ""
  }

  async function updateImages(action: () => Promise<unknown>, message: string) {
    if (busy) return
    setBusy(true)
    setError("")
    setSuccess("")
    try {
      await action()
      setSuccess(message)
      setReload((value) => value + 1)
      onImagesChanged()
    } catch (cause) {
      setError(errorMessage(cause))
    } finally {
      setBusy(false)
    }
  }

  async function upload(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    if (!file) return
    await updateImages(async () => {
      await uploadRoomImage(accessToken, roomId, file, isPrimary)
      setFile(null)
      setIsPrimary(false)
      if (inputRef.current) inputRef.current.value = ""
    }, "Đã tải ảnh phòng lên.")
  }

  return (
    <section
      className="mt-6"
      aria-labelledby="room-images-title"
      aria-busy={busy}
    >
      <div className="flex items-center justify-between gap-3">
        <h3 id="room-images-title" className="text-lg font-bold text-slate-900">
          Ảnh phòng
        </h3>
        <button
          type="button"
          disabled={busy || loading}
          onClick={() => {
            setError("")
            setReload((value) => value + 1)
          }}
          className="text-sm font-semibold text-[#087775] disabled:opacity-50"
        >
          Tải lại ảnh
        </button>
      </div>
      <p className="mt-1 text-sm text-slate-500">
        Ảnh đại diện hiển thị ở danh sách phòng cho khách. Ảnh đầu tiên tự động
        làm ảnh đại diện.
      </p>
      {error && (
        <p
          role="alert"
          className="mt-3 rounded-lg bg-rose-50 p-3 text-sm text-rose-700"
        >
          {error}
        </p>
      )}
      {success && (
        <p
          role="status"
          className="mt-3 rounded-lg bg-teal-50 p-3 text-sm text-[#087775]"
        >
          {success}
        </p>
      )}
      {loading ? (
        <p role="status" className="py-6 text-sm text-slate-500">
          Đang tải ảnh...
        </p>
      ) : images.length === 0 ? (
        <p className="py-6 text-sm text-slate-500">
          Phòng chưa có ảnh. Hãy tải ảnh đầu tiên.
        </p>
      ) : (
        <ul className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-2">
          {images.map((image, index) => (
            <li
              key={image.roomImageId}
              className="overflow-hidden rounded-xl border border-slate-200"
            >
              <a
                href={image.imageUrl}
                target="_blank"
                rel="noreferrer"
                aria-label={`Xem ảnh phòng ${index + 1}`}
              >
                <img
                  src={image.imageUrl}
                  alt={`Ảnh phòng ${index + 1}`}
                  className="h-36 w-full object-cover"
                />
              </a>
              <div className="flex flex-wrap items-center justify-between gap-2 p-3 text-xs font-semibold">
                {image.isPrimary ? (
                  <span className="text-[#087775]">Ảnh đại diện</span>
                ) : (
                  <button
                    type="button"
                    disabled={busy || loading}
                    onClick={() =>
                      void updateImages(
                        () =>
                          setPrimaryRoomImage(
                            accessToken,
                            roomId,
                            image.roomImageId,
                          ),
                        "Đã đổi ảnh đại diện.",
                      )
                    }
                    className="rounded-lg bg-teal-50 px-2 py-2 text-[#087775] disabled:opacity-50"
                  >
                    Đặt làm đại diện
                  </button>
                )}
                <button
                  type="button"
                  disabled={busy || loading}
                  aria-label={`Xóa ảnh phòng ${index + 1}`}
                  onClick={() => {
                    if (window.confirm("Xóa ảnh phòng này?"))
                      void updateImages(
                        () =>
                          deleteRoomImage(
                            accessToken,
                            roomId,
                            image.roomImageId,
                          ),
                        "Đã xóa ảnh phòng.",
                      )
                  }}
                  className="rounded-lg bg-rose-50 px-2 py-2 text-rose-700 disabled:opacity-50"
                >
                  Xóa
                </button>
              </div>
            </li>
          ))}
        </ul>
      )}
      <form
        onSubmit={(event) => void upload(event)}
        className="mt-4 space-y-3 rounded-xl border border-slate-200 p-4"
      >
        <label
          htmlFor="room-image-file"
          className="block text-sm font-semibold text-slate-700"
        >
          Chọn ảnh phòng
        </label>
        <input
          ref={inputRef}
          id="room-image-file"
          type="file"
          accept="image/jpeg,image/png,image/webp"
          disabled={busy || loading}
          onChange={(event) => selectFile(event.target.files?.[0])}
          aria-describedby="room-image-help"
          className="block w-full min-w-0 text-sm text-slate-600 file:mr-3 file:rounded-lg file:border-0 file:bg-teal-50 file:px-3 file:py-2 file:font-semibold file:text-[#087775]"
        />
        <p id="room-image-help" className="text-xs text-slate-500">
          JPEG, PNG hoặc WebP, tối đa 5 MiB mỗi ảnh.
        </p>
        {preview && (
          <img
            src={preview}
            alt="Xem trước ảnh phòng sẽ tải lên"
            className="max-h-52 w-full rounded-lg object-contain"
          />
        )}
        <label className="flex items-center gap-2 text-sm text-slate-700">
          <input
            type="checkbox"
            checked={isPrimary}
            disabled={busy}
            onChange={(event) => setIsPrimary(event.target.checked)}
          />
          Đặt làm ảnh đại diện
        </label>
        <button
          type="submit"
          disabled={!file || busy || loading}
          className="rounded-lg bg-[#087775] px-4 py-2 text-sm font-bold text-white disabled:opacity-50"
        >
          {busy ? "Đang xử lý..." : "Tải ảnh lên"}
        </button>
      </form>
    </section>
  )
}
