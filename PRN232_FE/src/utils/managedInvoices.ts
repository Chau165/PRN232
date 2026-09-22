import { managedInvoices, previousUtilityReadings } from "@/data/management"
import type {
  InvoiceAdditionalFee,
  ManagedInvoice,
} from "@/types/management"

const invoiceStorageKey = "troviet-managed-invoices"
const readingStorageKey = "troviet-utility-meter-readings"

export type CreateManagedInvoiceInput = {
  propertyId: string
  propertyName: string
  roomCode: string
  tenantName: string
  period: string
  roomRent: number
  electricityUsage: number
  electricityRate: number
  electricityReading: number
  waterUsage: number
  waterRate: number
  waterReading: number
  parkingFee: number
  internetFee: number
  trashFee: number
  additionalFees: InvoiceAdditionalFee[]
}

export function formatVnd(value: number) {
  return `${new Intl.NumberFormat("vi-VN").format(Math.round(value))}đ`
}

function getStoredInvoices(): ManagedInvoice[] {
  try {
    const stored = window.localStorage.getItem(invoiceStorageKey)
    return stored ? (JSON.parse(stored) as ManagedInvoice[]) : []
  } catch {
    return []
  }
}

export function getManagedInvoices(): ManagedInvoice[] {
  const storedInvoices = getStoredInvoices()
  const storedIds = new Set(storedInvoices.map((invoice) => invoice.id))
  return [
    ...storedInvoices,
    ...managedInvoices.filter((invoice) => !storedIds.has(invoice.id)),
  ]
}

export function getUtilityMeterReadings() {
  try {
    const stored = window.localStorage.getItem(readingStorageKey)
    const savedReadings = stored
      ? (JSON.parse(stored) as Record<
          string,
          { electricity: number; water: number }
        >)
      : {}
    return { ...previousUtilityReadings, ...savedReadings }
  } catch {
    return previousUtilityReadings
  }
}

function dueDateForPeriod(period: string) {
  const [monthText, yearText] = period.split("/")
  const month = Number(monthText)
  const year = Number(yearText)
  const dueMonth = month === 12 ? 1 : month + 1
  const dueYear = month === 12 ? year + 1 : year
  return `05/${String(dueMonth).padStart(2, "0")}/${dueYear}`
}

export function createManagedInvoice(input: CreateManagedInvoiceInput) {
  const additionalFees = input.additionalFees.filter(
    (fee) => fee.name.trim() && Number(fee.amount) > 0,
  )
  const electricityFee = input.electricityUsage * input.electricityRate
  const waterFee = input.waterUsage * input.waterRate
  const additionalFeeTotal = additionalFees.reduce(
    (sum, fee) => sum + Number(fee.amount),
    0,
  )
  const otherFees = input.parkingFee + input.trashFee + additionalFeeTotal
  const total =
    input.roomRent +
    electricityFee +
    waterFee +
    input.internetFee +
    otherFees
  const baseId = `INV-${input.period.replace("/", "-")}-${input.roomCode}`
  const duplicateCount = getManagedInvoices().filter((invoice) =>
    invoice.id.startsWith(baseId),
  ).length
  const invoice: ManagedInvoice = {
    id: duplicateCount ? `${baseId}-${duplicateCount + 1}` : baseId,
    propertyId: input.propertyId,
    propertyName: input.propertyName,
    roomCode: input.roomCode,
    tenantName: input.tenantName,
    period: input.period,
    roomRent: formatVnd(input.roomRent),
    electricity: formatVnd(electricityFee),
    water: formatVnd(waterFee),
    internet: formatVnd(input.internetFee),
    otherFees: formatVnd(otherFees),
    total: formatVnd(total),
    due: dueDateForPeriod(input.period),
    status: "UNPAID",
    electricityUsage: input.electricityUsage,
    electricityRate: input.electricityRate,
    waterUsage: input.waterUsage,
    waterRate: input.waterRate,
    parkingFee: formatVnd(input.parkingFee),
    trashFee: formatVnd(input.trashFee),
    additionalFees: additionalFees.map((fee) => ({
      name: fee.name.trim(),
      amount: formatVnd(Number(fee.amount)),
    })),
  }

  window.localStorage.setItem(
    invoiceStorageKey,
    JSON.stringify([invoice, ...getStoredInvoices()]),
  )
  const readings = getUtilityMeterReadings()
  window.localStorage.setItem(
    readingStorageKey,
    JSON.stringify({
      ...readings,
      [`room-${input.roomCode.toLowerCase()}`]: {
        electricity: input.electricityReading,
        water: input.waterReading,
      },
    }),
  )
  return invoice
}
