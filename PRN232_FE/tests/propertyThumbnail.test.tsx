import { fireEvent, render, screen } from "@testing-library/react"
import { describe, expect, it } from "vitest"
import PropertyThumbnail from "@/components/management/PropertyThumbnail"

describe("management property thumbnail", () => {
  it.each([undefined, null, "", "   "])("does not invent an image when the API returns %s", (imageUrl) => {
    render(<PropertyThumbnail name="Nhà trọ" imageUrl={imageUrl} />)
    expect(screen.queryByRole("img")).toBeNull()
    expect(screen.getByText("Chưa có ảnh")).toBeTruthy()
  })

  it("shows a newly uploaded URL after an old image fails", () => {
    const view = render(<PropertyThumbnail name="Nhà trọ" imageUrl="https://images.example.test/old.jpg" />)
    fireEvent.error(screen.getByRole("img", { name: "Ảnh Nhà trọ" }))
    expect(screen.getByText("Chưa có ảnh")).toBeTruthy()
    view.rerender(<PropertyThumbnail name="Nhà trọ" imageUrl="https://images.example.test/new.jpg" />)
    expect(screen.getByRole("img", { name: "Ảnh Nhà trọ" }).getAttribute("src")).toBe("https://images.example.test/new.jpg")
  })
})
