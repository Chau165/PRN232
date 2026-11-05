import { describe, expect, it } from "vitest"
import { ApiError } from "@/api/apiClient"
import { getPropertyManagerAssignmentError } from "@/utils/propertyManagerAssignmentErrors"

describe("property manager assignment API errors", () => {
  it("preserves actionable authorization, not-found, and conflict details", () => {
    expect(getPropertyManagerAssignmentError(new ApiError("OwnerId=2 đang trỏ tới Manager", { status: 403 })))
      .toContain("OwnerId=2")
    expect(getPropertyManagerAssignmentError(new ApiError("Không tìm thấy Manager", { status: 404 })))
      .toBe("Không tìm thấy Manager")
    expect(getPropertyManagerAssignmentError(new ApiError("Còn lịch xem chưa bàn giao", { status: 409 })))
      .toBe("Còn lịch xem chưa bàn giao")
  })

  it("provides clear validation, session, and server-error messages", () => {
    expect(getPropertyManagerAssignmentError(new ApiError("Invalid request", {
      status: 400,
      validationErrors: { ManagerId: ["ManagerId phải lớn hơn 0."] },
    }))).toContain("ManagerId phải lớn hơn 0.")
    expect(getPropertyManagerAssignmentError(new ApiError("Unauthorized", { status: 401 })))
      .toContain("đăng nhập lại")
    expect(getPropertyManagerAssignmentError(new ApiError("Internal failure", { status: 500 })))
      .toContain("Máy chủ đang gặp lỗi")
  })
})
