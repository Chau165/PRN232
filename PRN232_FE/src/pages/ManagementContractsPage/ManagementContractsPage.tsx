import { useEffect, useState } from "react"
import { useLocation } from "react-router"
import ManagementDataTable from "@/components/management/ManagementDataTable"
import ManagementDetailDrawer from "@/components/management/ManagementDetailDrawer"
import ManagementPage from "@/components/management/ManagementPage"
import StatusBadge from "@/components/customer/StatusBadge"
import Toast from "@/components/customer/Toast"
import { useAuth } from "@/auth/AuthContext"
import { getContracts } from "@/services/contractService"
import type { ContractDto } from "@/types/contract"
import { formatContractDate } from "@/utils/managementContracts"
import { formatVnd } from "@/utils/publicFormatting"

function statusLabel(status: string) {
  return status === "Active" ? "Đang hiệu lực" : status
}

export default function ManagementContractsPage() {
  const { accessToken } = useAuth()
  const location = useLocation()
  const [contracts, setContracts] = useState<ContractDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [reload, setReload] = useState(0)
  const [selected, setSelected] = useState<ContractDto | null>(null)
  // Trang "Tạo hợp đồng" chuyển sang đây kèm id hợp đồng vừa tạo để báo thành công.
  const createdContractId = (location.state as { createdContractId?: number } | null)?.createdContractId
  const [toast, setToast] = useState(createdContractId ? `Đã tạo hợp đồng #${createdContractId} thành công.` : "")

  useEffect(() => {
    if (!accessToken) return
    const controller = new AbortController()
    setLoading(true)
    setError(null)
    getContracts(accessToken, controller.signal)
      .then(setContracts)
      .catch((fetchError: unknown) => {
        if (!controller.signal.aborted) {
          setError(fetchError instanceof Error ? fetchError.message : "Không thể tải danh sách hợp đồng.")
        }
      })
      .finally(() => {
        if (!controller.signal.aborted) setLoading(false)
      })
    return () => controller.abort()
  }, [accessToken, reload])

  return (
    <ManagementPage title="Hợp đồng" description="Quản lý hợp đồng thuê được lập từ các booking đã đặt cọc.">
      {loading && <p role="status" className="text-[14px] text-slate-500">Đang tải danh sách hợp đồng...</p>}
      {!loading && error && (
        <div role="alert" className="rounded-2xl bg-white p-6 text-center shadow-[0_3px_18px_rgba(16,65,67,.05)]">
          <p className="text-[14px] text-slate-700">{error}</p>
          <button type="button" onClick={() => setReload((value) => value + 1)} className="mt-4 rounded-lg bg-[#087775] px-4 py-2 text-[13px] font-bold text-white">
            Thử lại
          </button>
        </div>
      )}
      {!loading && !error && (
        <ManagementDataTable headers={["Hợp đồng", "Khách thuê", "Khu trọ / Phòng", "Thời hạn", "Giá thuê", "Trạng thái", "Thao tác"]}>
          {contracts.length === 0 ? (
            <p className="px-5 py-8 text-center text-[13px] text-slate-500">Chưa có hợp đồng nào.</p>
          ) : (
            <div className="divide-y divide-slate-100">
              {contracts.map((contract) => (
                <div key={contract.contractId} className="grid gap-3 px-5 py-5 lg:grid-cols-7 lg:items-center">
                  <strong className="text-[13px] text-slate-700">HĐ #{contract.contractId}</strong>
                  <span className="text-[13px] text-slate-600">{contract.customerName}</span>
                  <span className="text-[13px] text-slate-600">
                    {contract.propertyName}
                    <small className="block text-slate-400">Phòng {contract.roomCode}</small>
                  </span>
                  <span className="text-[13px] text-slate-600">
                    {formatContractDate(contract.startDate)}
                    <small className="block text-slate-400">đến {formatContractDate(contract.endDate)}</small>
                  </span>
                  <span className="font-semibold text-[#087775]">{formatVnd(contract.monthlyRent)}</span>
                  <StatusBadge tone={contract.status === "Active" ? "green" : "slate"}>{statusLabel(contract.status)}</StatusBadge>
                  <div>
                    <button
                      type="button"
                      onClick={() => setSelected(contract)}
                      className="rounded-lg bg-slate-100 px-3 py-2 text-[12px] font-bold text-slate-600"
                    >
                      Xem
                    </button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </ManagementDataTable>
      )}
      {selected && (
        <ManagementDetailDrawer title="Chi tiết hợp đồng" eyebrow={`HĐ #${selected.contractId}`} onClose={() => setSelected(null)}>
          <dl className="space-y-3 rounded-xl bg-slate-50 p-5 text-[14px]">
            <Detail label="Khách thuê" value={selected.customerName} />
            <Detail label="Số điện thoại" value={selected.customerPhone || "Chưa có thông tin"} />
            {selected.bookingId && <Detail label="Booking" value={`#${selected.bookingId}`} />}
            <Detail label="Khu trọ" value={selected.propertyName} />
            <Detail label="Phòng" value={selected.roomCode} />
            <Detail label="Thời hạn" value={`${formatContractDate(selected.startDate)} - ${formatContractDate(selected.endDate)}`} />
            <Detail label="Giá thuê" value={`${formatVnd(selected.monthlyRent)}/tháng`} />
            <Detail label="Tiền cọc" value={formatVnd(selected.depositAmount)} />
            {selected.processedByStaffName && <Detail label="Người lập" value={selected.processedByStaffName} />}
          </dl>
        </ManagementDetailDrawer>
      )}
      {toast && <Toast message={toast} onClose={() => setToast("")} />}
    </ManagementPage>
  )
}

function Detail({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex justify-between gap-4">
      <dt className="text-slate-500">{label}</dt>
      <dd className="text-right font-semibold text-slate-800">{value}</dd>
    </div>
  )
}
