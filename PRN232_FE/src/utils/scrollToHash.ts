export function scrollToHash(hash: string) {
  const target = document.getElementById(hash.replace(/^#/, ""))
  target?.scrollIntoView({ behavior: "smooth", block: "start" })
}
