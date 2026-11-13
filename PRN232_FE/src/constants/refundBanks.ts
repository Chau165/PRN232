// Public bank BIN directory: https://api.vietqr.io/v2/banks (checked 2026-11-13).
// Account information is never sent to this directory by the frontend.
export const refundBanks = [
  { bin: "970422", name: "MB Bank" },
  { bin: "970436", name: "Vietcombank" },
  { bin: "970418", name: "BIDV" },
  { bin: "970407", name: "Techcombank" },
  { bin: "970416", name: "ACB" },
  { bin: "970432", name: "VPBank" },
  { bin: "970423", name: "TPBank" },
  { bin: "970405", name: "Agribank" },
  { bin: "970403", name: "Sacombank" },
  { bin: "970441", name: "VIB" },
] as const
export function refundBankName(bin: string) { return refundBanks.find(bank => bank.bin === bin)?.name ?? `Ngân hàng ${bin}` }
