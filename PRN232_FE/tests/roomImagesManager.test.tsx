import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { beforeEach, describe, expect, it, vi } from "vitest"
import RoomImagesManager from "@/components/management/RoomImagesManager"

const first = {
  roomImageId: 10,
  roomId: 31,
  imageUrl: "https://images.test/room.jpg",
  isPrimary: true,
}
const second = {
  roomImageId: 11,
  roomId: 31,
  imageUrl: "https://images.test/room2.jpg",
  isPrimary: false,
}
const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" },
  })

describe("room image management", () => {
  beforeEach(() => {
    vi.stubGlobal(
      "URL",
      Object.assign(URL, {
        createObjectURL: vi.fn(() => "blob:preview"),
        revokeObjectURL: vi.fn(),
      }),
    )
  })

  it("uploads the selected file as multipart with auth and refreshes the gallery", async () => {
    const changed = vi.fn()
    let uploaded = false
    const fetchMock = vi.fn(
      async (input: RequestInfo | URL, init?: RequestInit) => {
        if (String(input).endsWith("/images/upload")) {
          expect(init?.method).toBe("POST")
          expect(new Headers(init?.headers).get("Authorization")).toBe(
            "Bearer test-token",
          )
          expect(new Headers(init?.headers).get("Content-Type")).toBeNull()
          expect(init?.body).toBeInstanceOf(FormData)
          expect((init?.body as FormData).get("isPrimary")).toBe("true")
          expect(((init?.body as FormData).get("file") as File).name).toBe(
            "room.png",
          )
          uploaded = true
          return json(first)
        }
        return json(uploaded ? [first] : [])
      },
    )
    vi.stubGlobal("fetch", fetchMock)
    render(
      <RoomImagesManager
        accessToken="test-token"
        roomId={31}
        onImagesChanged={changed}
      />,
    )
    await screen.findByText("Phòng chưa có ảnh. Hãy tải ảnh đầu tiên.")
    fireEvent.change(screen.getByLabelText("Chọn ảnh phòng"), {
      target: { files: [new File(["png"], "room.png", { type: "image/png" })] },
    })
    expect(
      await screen.findByAltText("Xem trước ảnh phòng sẽ tải lên"),
    ).toBeTruthy()
    fireEvent.click(screen.getByLabelText("Đặt làm ảnh đại diện"))
    fireEvent.click(screen.getByRole("button", { name: "Tải ảnh lên" }))
    expect(await screen.findByAltText("Ảnh phòng 1")).toBeTruthy()
    expect(changed).toHaveBeenCalledOnce()
    expect(URL.revokeObjectURL).toHaveBeenCalledWith("blob:preview")
  })

  it("rejects unsupported and oversized files without uploading", async () => {
    const fetchMock = vi.fn(async () => json([]))
    vi.stubGlobal("fetch", fetchMock)
    render(
      <RoomImagesManager
        accessToken="token"
        roomId={31}
        onImagesChanged={() => {}}
      />,
    )
    await screen.findByText("Phòng chưa có ảnh. Hãy tải ảnh đầu tiên.")
    fireEvent.change(screen.getByLabelText("Chọn ảnh phòng"), {
      target: {
        files: [new File(["svg"], "room.svg", { type: "image/svg+xml" })],
      },
    })
    expect(screen.getByRole("alert").textContent).toContain(
      "JPEG, PNG hoặc WebP",
    )
    fireEvent.change(screen.getByLabelText("Chọn ảnh phòng"), {
      target: {
        files: [
          new File([new Uint8Array(5 * 1024 * 1024 + 1)], "big.png", {
            type: "image/png",
          }),
        ],
      },
    })
    expect(screen.getByRole("alert").textContent).toContain("5 MiB")
    expect(
      (screen.getByRole("button", { name: "Tải ảnh lên" }) as HTMLButtonElement)
        .disabled,
    ).toBe(true)
    expect(fetchMock).toHaveBeenCalledOnce()
  })

  it("sets the primary image and deletes an image using room-scoped endpoints", async () => {
    const changed = vi.fn()
    const fetchMock = vi.fn(
      async (_input: RequestInfo | URL, init?: RequestInit) =>
        init?.method
          ? new Response(null, { status: 204 })
          : json([first, second]),
    )
    vi.stubGlobal("fetch", fetchMock)
    vi.spyOn(window, "confirm").mockReturnValue(true)
    render(
      <RoomImagesManager
        accessToken="token"
        roomId={31}
        onImagesChanged={changed}
      />,
    )
    fireEvent.click(
      await screen.findByRole("button", { name: "Đặt làm đại diện" }),
    )
    await waitFor(() => expect(changed).toHaveBeenCalledTimes(1))
    await waitFor(() =>
      expect(
        (screen.getByRole("button", {
          name: "Xóa ảnh phòng 2",
        }) as HTMLButtonElement).disabled,
      ).toBe(false),
    )
    fireEvent.click(screen.getByRole("button", { name: "Xóa ảnh phòng 2" }))
    await waitFor(() => expect(changed).toHaveBeenCalledTimes(2))
    expect(
      fetchMock.mock.calls.some(
        ([url, init]) =>
          String(url) === "/api/rooms/31/images/11/primary" &&
          init?.method === "PUT",
      ),
    ).toBe(true)
    expect(
      fetchMock.mock.calls.some(
        ([url, init]) =>
          String(url) === "/api/rooms/31/images/11" &&
          init?.method === "DELETE",
      ),
    ).toBe(true)
  })

  it("shows upload failures and preserves the selected file for retry", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn(async (_input: RequestInfo | URL, init?: RequestInit) =>
        init?.method ? json({ detail: "Storage unavailable" }, 502) : json([]),
      ),
    )
    const changed = vi.fn()
    render(
      <RoomImagesManager
        accessToken="token"
        roomId={31}
        onImagesChanged={changed}
      />,
    )
    await screen.findByText("Phòng chưa có ảnh. Hãy tải ảnh đầu tiên.")
    fireEvent.change(screen.getByLabelText("Chọn ảnh phòng"), {
      target: { files: [new File(["png"], "room.png", { type: "image/png" })] },
    })
    fireEvent.click(screen.getByRole("button", { name: "Tải ảnh lên" }))
    expect((await screen.findByRole("alert")).textContent).toContain(
      "Storage unavailable",
    )
    expect(changed).not.toHaveBeenCalled()
    expect(screen.getByAltText("Xem trước ảnh phòng sẽ tải lên")).toBeTruthy()
  })
})
