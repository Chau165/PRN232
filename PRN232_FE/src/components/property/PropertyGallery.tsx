type PropertyGalleryProps = {
  photos: string[]
  onOpen: () => void
}

export default function PropertyGallery({
  photos,
  onOpen,
}: PropertyGalleryProps) {
  return (
    <div className="gallery-grid mt-8 h-[440px] overflow-hidden rounded-3xl">
      {photos.map((photo, index) => (
        <div
          key={photo}
          className={`relative overflow-hidden bg-slate-200 ${
            index === 0 ? "gallery-main" : ""
          }`}
        >
          <img
            src={photo}
            alt={`Không gian Sunrise Residence ${index + 1}`}
            className="h-full w-full object-cover transition duration-500 hover:scale-105"
          />
          {index === 4 && (
            <button
              onClick={onOpen}
              className="absolute bottom-4 right-4 rounded-lg bg-white px-3.5 py-2.5 text-[12px] font-bold text-slate-700 shadow-sm"
            >
              Xem tất cả ảnh
            </button>
          )}
        </div>
      ))}
    </div>
  )
}
