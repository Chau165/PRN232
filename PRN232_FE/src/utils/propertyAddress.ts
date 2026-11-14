import { formatAddress } from "@/utils/publicFormatting"

export function normalizeAdministrativeName(value: string): string {
  const tokens = value
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/gi, "d")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, " ")
    .trim()
    .split(/\s+/)
    .filter(Boolean)

  const prefixes = [
    ["khu", "vuc"],
    ["thanh", "pho"],
    ["dac", "khu"],
    ["province", "of"],
    ["city", "of"],
    ["tp"],
    ["tinh"],
    ["phuong"],
    ["xa"],
    ["quan"],
    ["huyen"],
    ["ward"],
    ["commune"],
    ["district"],
    ["province"],
    ["city"],
  ]

  let changed = true
  while (changed && tokens.length > 1) {
    changed = false
    for (const prefix of prefixes) {
      if (prefix.every((token, index) => tokens[index] === token)) {
        tokens.splice(0, prefix.length)
        changed = true
        break
      }
    }
  }

  while (tokens.length > 1 && ["cu", "city", "province", "ward"].includes(tokens.at(-1) ?? "")) {
    tokens.pop()
  }

  return tokens.join(" ")
}

function knownAdministrativeNames(parts: readonly string[]): Set<string> {
  return new Set(parts.map(normalizeAdministrativeName).filter(Boolean))
}

export function buildFullPropertyAddress(
  streetAddress: string,
  wardName: string,
  provinceName: string,
): string {
  return formatAddress(streetAddress, wardName, provinceName, "Việt Nam")
}

export function hasDuplicateAdministrativeSegments(
  addressText: string,
  administrativeParts: readonly string[],
): boolean {
  const knownNames = knownAdministrativeNames(administrativeParts)
  return addressText
    .split(",")
    .slice(1)
    .some((segment) => knownNames.has(normalizeAdministrativeName(segment)))
}

export function removeKnownAdministrativeSegments(
  addressText: string,
  administrativeParts: readonly string[],
): string {
  const knownNames = knownAdministrativeNames(administrativeParts)
  return addressText
    .split(",")
    .map((segment) => segment.trim())
    .filter((segment, index) =>
      index === 0 || !knownNames.has(normalizeAdministrativeName(segment)),
    )
    .filter(Boolean)
    .join(", ")
}
