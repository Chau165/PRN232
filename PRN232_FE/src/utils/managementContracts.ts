export function isContractDate(value: string) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(value)) return false
  const date = new Date(`${value}T00:00:00Z`)
  return !Number.isNaN(date.getTime()) && date.toISOString().slice(0, 10) === value
}

export function formatContractDate(value: string) {
  return value.split("-").reverse().join("/")
}

// Keep the same calendar day; clamp to the last day for shorter months.
export function addContractMonths(start: string, months: number) {
  if (!isContractDate(start)) return ""
  const date = new Date(`${start}T00:00:00Z`)
  const day = date.getUTCDate()
  date.setUTCDate(1)
  date.setUTCMonth(date.getUTCMonth() + months)
  const lastDay = new Date(Date.UTC(date.getUTCFullYear(), date.getUTCMonth() + 1, 0)).getUTCDate()
  date.setUTCDate(Math.min(day, lastDay))
  return date.toISOString().slice(0, 10)
}

// Ngày hôm nay theo lịch của trình duyệt, dạng "yyyy-MM-dd" như ô <input type="date">.
export function localToday(now = new Date()) {
  return new Date(now.getTime() - now.getTimezoneOffset() * 60_000).toISOString().slice(0, 10)
}
