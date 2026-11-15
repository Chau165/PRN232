// Some Vietnamese keyboards type "ễ" as "ê" + a separate combining tilde (Unicode tổ hợp).
// It looks the same but does not match map data or saved rows, so compose it (NFC) and
// collapse repeated whitespace before searching or saving.
export function cleanText(value: string) {
  return value.normalize("NFC").replace(/\s+/g, " ").trim()
}
