import { useState, type FormEvent } from "react"
import type { Invoice, InvoiceComplaintForm } from "@/types/customer"

type InvoiceComplaintModalProps = {
  invoice: Invoice
  onClose: () => void
  onSubmit: (form: InvoiceComplaintForm) => void
}

export default function InvoiceComplaintModal({
  invoice,
  onClose,
  onSubmit,
}: InvoiceComplaintModalProps) {
  const [feeType, setFeeType] = useState("")
  const [content, setContent] = useState("")

  function submit(event: FormEvent<HTMLFormElement>) {
    event.preventDefault()
    onSubmit({
      invoiceId: invoice.id,
      feeType,
      content: content.trim(),
    })
  }

  return (
    <div
      className="fixed inset-0 z-[70] grid place-items-center bg-slate-950/45 p-5"
      role="presentation"
      onMouseDown={(event) => {
        if (event.target === event.currentTarget) onClose()
      }}
    >
      <form
        onSubmit={submit}
        className="w-full max-w-[540px] rounded-2xl bg-white p-7 shadow-2xl"
        aria-labelledby="invoice-complaint-title"
      >
        <div className="flex items-start justify-between gap-5">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              {invoice.id}
            </p>
            <h2
              id="invoice-complaint-title"
              className="mt-2 font-display text-[24px] font-bold tracking-[-.04em] text-slate-900"
            >
              Gửi khiếu nại hóa đơn
            </h2>
            <p className="mt-2 text-[13px] leading-5 text-slate-500">
              Hãy cho chúng tôi biết khoản phí cần kiểm tra để quản lý hỗ trợ
              bạn sớm nhất.
            </p>
          </div>
          <button
            type="button"
            onClick={onClose}
            aria-label="Đóng"
            className="grid h-8 w-8 shrink-0 place-items-center rounded-full bg-slate-50 text-lg text-slate-500 hover:bg-slate-100"
          >
            ×
          </button>
        </div>

        <div className="mt-6 space-y-5">
          <label className="block text-[13px] font-semibold text-slate-700">
            Loại phí
            <select
              required
              value={feeType}
              onChange={(event) => setFeeType(event.target.value)}
              className="mt-2 h-11 w-full rounded-xl border border-slate-200 bg-white px-3.5 text-[14px] text-slate-700 outline-none focus:border-[#087775] focus:ring-2 focus:ring-[#087775]/15"
            >
              <option value="" disabled>
                Chọn loại phí cần khiếu nại
              </option>
              {invoice.items.map((item) => (
                <option key={item.label} value={item.label}>
                  {item.label} — {item.amount}
                </option>
              ))}
            </select>
          </label>

          <label className="block text-[13px] font-semibold text-slate-700">
            Nội dung khiếu nại
            <textarea
              required
              value={content}
              onChange={(event) => setContent(event.target.value)}
              rows={5}
              placeholder="Ví dụ: Chỉ số điện trên hóa đơn chưa đúng với chỉ số công tơ..."
              className="mt-2 w-full resize-none rounded-xl border border-slate-200 bg-white px-3.5 py-3 text-[14px] leading-5 text-slate-700 outline-none placeholder:text-slate-400 focus:border-[#087775] focus:ring-2 focus:ring-[#087775]/15"
            />
          </label>
        </div>

        <div className="mt-7 grid grid-cols-2 gap-3">
          <button
            type="button"
            onClick={onClose}
            className="rounded-lg bg-slate-100 py-3 text-[13px] font-bold text-slate-600 hover:bg-slate-200"
          >
            Hủy
          </button>
          <button
            type="submit"
            className="rounded-lg bg-[#087775] py-3 text-[13px] font-bold text-white shadow-sm hover:bg-[#066a68]"
          >
            Gửi khiếu nại
          </button>
        </div>
      </form>
    </div>
  )
}
