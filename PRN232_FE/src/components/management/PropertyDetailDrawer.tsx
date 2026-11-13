import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import PropertyServiceRatesPanel from "@/components/management/PropertyServiceRatesPanel"
import type { PropertyDto } from "@/types/user"

type PropertyDetailDrawerProps = {
  property: PropertyDto
  onClose: () => void
}

export default function PropertyDetailDrawer({
  property,
  onClose,
}: PropertyDetailDrawerProps) {
  return (
    <ManagementDetailDrawer
      title={property.propertyName}
      eyebrow="Chi tiết khu trọ"
      onClose={onClose}
    >
      <div className="space-y-4 rounded-xl bg-slate-50 p-4 text-sm text-slate-700">
        <p>
          <span className="text-slate-400">Chủ sở hữu:</span>{" "}
          <strong>{property.ownerName}</strong>
        </p>
        <p>
          <span className="text-slate-400">Địa chỉ:</span>{" "}
          <strong>{property.addressText}</strong>
        </p>
        <p>
          <span className="text-slate-400">Khu vực:</span>{" "}
          <strong>
            {[property.ward, property.district, property.city]
              .filter(Boolean)
              .join(", ") || "Chưa có"}
          </strong>
        </p>
        <p>
          <span className="text-slate-400">Tọa độ:</span>{" "}
          <strong>
            {property.latitude ?? 0}, {property.longitude ?? 0}
          </strong>
        </p>
        <p>
          <span className="text-slate-400">Số phòng:</span>{" "}
          <strong>{property.roomCount}</strong>
        </p>
        <p>
          <span className="text-slate-400">Mô tả:</span>{" "}
          <strong>{property.description || "Chưa có mô tả"}</strong>
        </p>
        {property.primaryImageUrl && (
          <img
            src={property.primaryImageUrl}
            alt={property.propertyName}
            className="max-h-56 w-full rounded-lg object-cover"
          />
        )}
      </div>
      <PropertyServiceRatesPanel propertyId={property.propertyId} />
    </ManagementDetailDrawer>
  )
}
