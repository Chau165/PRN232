import { test } from "node:test"
import assert from "node:assert/strict"
import { checkRelease } from "./check-booking-identity-release.mjs"
const env = { IDENTITY_BE_URL: "https://backend.test", Supabase__Url: "https://project.supabase.co",
  Supabase__BookingIdentityBucket: "booking-identities", Supabase__ServiceRoleKey: "test-secret" }
function wire(isPublic = false) {
  return async (url, options) => {
    assert.equal(options.redirect, "error")
    if (url.startsWith("https://project.supabase.co")) {
      assert.equal(options.headers.apikey, "test-secret")
      return Response.json({ public: isPublic })
    }
    assert.equal(options.headers, undefined)
    if (url.endsWith("booking-application-reviews")) return new Response(null, { status: 401 })
    if (url.endsWith("swagger.json")) return Response.json({ paths: { "/api/booking-applications/{id}/people/{personId}/images/{side}": { post: {} } } })
    return Response.json({ status: "ok" })
  }
}
test("reports public readiness separately from optional private bucket configuration", async () => {
  const checks = await checkRelease(env, wire(), true)
  assert.equal(checks.length, 5); assert.ok(checks.every(c => c.ok))
  assert.ok(!JSON.stringify(checks).includes("test-secret"))
})
test("rejects public buckets and never sends credentials to a non-Supabase host", async () => {
  assert.equal((await checkRelease(env, wire(true), true)).at(-1).ok, false)
  assert.equal((await checkRelease({ ...env, Supabase__Url: "https://foreign.test" }, wire(), true)).at(-1).code, "MISSING_OR_INVALID_STORAGE_CONFIGURATION")
})
test("does not treat unreachable DB or absent schema as a pass", async () => {
  const checks = await checkRelease(env, async () => new Response(null, { status: 503 }))
  assert.ok(checks.every(c => !c.ok))
})
