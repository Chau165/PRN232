import { render, screen } from "@testing-library/react"
import { afterEach, describe, expect, it, vi } from "vitest"
import { readFileSync } from "node:fs"
import { resolve } from "node:path"

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })
}

afterEach(() => {
  vi.unstubAllGlobals()
  vi.resetModules()
  window.history.pushState({}, "", "/")
})

describe("route code splitting (C6)", () => {
  it("imports only the landing page statically", () => {
    const source = readFileSync(resolve(__dirname, "../src/app/router.tsx"), "utf-8")
    const staticPageImports = [...source.matchAll(/^import \w+ from "@\/pages\/([^"]+)"/gm)]
      .map((match) => match[1])

    expect(staticPageImports).toEqual(["HomePage/HomePage"])
    expect(source).toContain('lazy(() => import("@/pages/ManagementUsersPage/ManagementUsersPage"))')
  })

  it("shows the loading fallback, then renders the lazily loaded route", async () => {
    vi.stubGlobal("fetch", vi.fn<typeof fetch>(async () => jsonResponse({ title: "Not Found" }, 404)))
    // createBrowserRouter reads the URL when the module is evaluated, so navigate first.
    window.history.pushState({}, "", "/login")
    const { default: App } = await import("@/App")

    render(<App />)

    expect(screen.getByRole("status").textContent).toContain("Đang tải")
    expect(await screen.findByLabelText("Email")).toBeTruthy()
    expect(screen.queryByText("Đang tải...")).toBeNull()
  })
})
