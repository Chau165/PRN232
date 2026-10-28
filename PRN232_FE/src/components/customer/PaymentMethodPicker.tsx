import type { PaymentMethod } from "@/api/bookingsApi"

const paymentMethods: PaymentMethod[] = ["VNPay", "MoMo"]

type PaymentMethodPickerProps = {
  value: PaymentMethod | null
  onChange: (method: PaymentMethod) => void
}

// Dùng chung cho thanh toán tiền cọc và thanh toán hoá đơn (cùng cổng giả lập ở BE).
export default function PaymentMethodPicker({ value, onChange }: PaymentMethodPickerProps) {
  return (
    <fieldset>
      <legend className="font-display text-lg font-bold text-slate-900">Phương thức thanh toán</legend>
      <div className="mt-3 grid grid-cols-2 gap-3">
        {paymentMethods.map((item) => (
          <label
            key={item}
            className={`cursor-pointer rounded-xl border p-4 text-center text-sm font-semibold ${value === item ? "border-[#087775] bg-teal-50 text-[#087775]" : "border-slate-200 text-slate-600"}`}
          >
            <input
              type="radio"
              name="paymentMethod"
              value={item}
              checked={value === item}
              onChange={() => onChange(item)}
              className="sr-only"
            />
            {item}
          </label>
        ))}
      </div>
      <p className="mt-2 text-xs text-slate-400">Cổng thanh toán giả lập cho đồ án, không trừ tiền thật.</p>
    </fieldset>
  )
}
