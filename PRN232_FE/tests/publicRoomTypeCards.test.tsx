import { render, screen } from "@testing-library/react"
import { MemoryRouter } from "react-router"
import { beforeEach, describe, expect, it, vi } from "vitest"
import PublicRoomTypeCards from "@/components/property/PublicRoomTypeCards"

const mocks = vi.hoisted(() => ({ listRoomTypes: vi.fn() }))

vi.mock("@/services/publicBrowsingService", () => ({
  publicApi: { listRoomTypes: mocks.listRoomTypes },
}))

describe("PublicRoomTypeCards", () => {
  beforeEach(() => {
    mocks.listRoomTypes.mockReset()
    mocks.listRoomTypes.mockResolvedValue([
      {
        propertyId: 7,
        propertyName: "Khu A",
        roomTypeId: 2,
        roomTypeName: "Phòng cao cấp",
        monthlyRent: 3_500_000,
        availableRoomCount: 3,
        totalRoomCount: 5,
        minimumAreaSqm: 18,
        maximumAreaSqm: 24,
        referenceImageUrl: null,
      },
    ])
  })

  it("shows property-scoped rent and requests a viewing by property and type", async () => {
    render(
      <MemoryRouter>
        <PublicRoomTypeCards propertyId={7} />
      </MemoryRouter>,
    )

    expect(await screen.findByText("Phòng cao cấp")).toBeTruthy()
    expect(screen.getByText("3/5 phòng hiện có thể thuê")).toBeTruthy()
    const viewingLink = screen.getByRole("link", { name: "Đặt lịch xem" })
    expect(viewingLink.getAttribute("href")).toBe("/viewing-request?propertyId=7&roomTypeId=2")
    expect(mocks.listRoomTypes).toHaveBeenCalledWith(7, {}, expect.any(AbortSignal))
  })
})
