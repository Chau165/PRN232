// Read-only production checks. No ID images, DB writes, checkout or money transfers.
// Run with Node 20+: node PRN232_BE/scripts/check-booking-identity-release.mjs [--storage]
import { pathToFileURL } from "node:url"
export async function checkRelease(env, fetcher = fetch, checkStorage = false) {
  const checks = []
  const report = (name, ok, code) => checks.push({ name, ok, code })
  function origin(value) {
    const url = new URL(value)
    if (url.protocol !== "https:" || url.username || url.password || url.search || url.hash || url.pathname !== "/") throw Error()
    return url.origin
  }
  let base
  try { base = origin(env.IDENTITY_BE_URL || "https://prn232-production.up.railway.app") }
  catch { report("backend", false, "INVALID_HTTPS_ORIGIN"); return checks }
  async function get(url, headers) {
    return fetcher(url, { headers, redirect: "error", cache: "no-store", signal: AbortSignal.timeout(15000) })
  }
  for (const path of ["/api/health-checks", "/api/health-checks/database"]) {
    try { const r = await get(base + path); report(path, r.status === 200, `HTTP_${r.status}`) }
    catch { report(path, false, "UNREACHABLE") }
  }
  try {
    const r = await get(base + "/api/booking-application-reviews")
    report("anonymous_review_denied", r.status === 401, `HTTP_${r.status}`)
  } catch { report("anonymous_review_denied", false, "UNREACHABLE") }
  try {
    const r = await get(base + "/swagger/v1/swagger.json")
    const schema = r.ok ? await r.json() : null
    report("identity_upload_route", !!schema?.paths?.["/api/booking-applications/{id}/people/{personId}/images/{side}"]?.post,
      r.ok ? "SCHEMA_CHECKED" : `HTTP_${r.status}`)
  } catch { report("identity_upload_route", false, "SWAGGER_UNAVAILABLE") }
  if (checkStorage) {
    const bucket = env.Supabase__BookingIdentityBucket
    const key = env.Supabase__ServiceRoleKey
    let storage
    try {
      storage = origin(env.Supabase__Url)
      if (!new URL(storage).hostname.endsWith(".supabase.co") || !/^[a-z0-9][a-z0-9-]{0,62}$/.test(bucket || "") || !key) throw Error()
    } catch { report("private_storage", false, "MISSING_OR_INVALID_STORAGE_CONFIGURATION"); return checks }
    try {
      const r = await get(`${storage}/storage/v1/bucket/${bucket}`, { apikey: key, Authorization: `Bearer ${key}` })
      const metadata = r.ok ? await r.json() : null
      report("private_storage", r.ok && metadata?.public === false, r.ok ? (metadata?.public === false ? "PRIVATE_BUCKET" : "BUCKET_NOT_PRIVATE") : `HTTP_${r.status}`)
    } catch { report("private_storage", false, "STORAGE_UNREACHABLE") }
  }
  return checks
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  const checks = await checkRelease(process.env, fetch, process.argv.includes("--storage"))
  console.log(JSON.stringify({ checks, allCheckedPassed: checks.every(c => c.ok),
    privateUploadAndPaymentVerified: false, note: "Read-only preflight; authenticated UAT and schema checks are separate." }, null, 2))
  process.exitCode = checks.every(c => c.ok) ? 0 : 1
}
