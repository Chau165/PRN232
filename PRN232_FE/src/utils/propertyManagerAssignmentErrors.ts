import { ApiError } from "@/api/apiClient"

export function getPropertyManagerAssignmentError(error: unknown): string {
  if (!(error instanceof ApiError)) {
    return error instanceof Error ? error.message : "Không thể cập nhật phân công Manager."
  }

  switch (error.status) {
    case 400: {
      const validationMessages = Object.values(error.validationErrors ?? {}).flat()
      return validationMessages.length > 0
        ? `Dữ liệu phân công không hợp lệ: ${validationMessages.join(" ")}`
        : error.message
    }
    case 401:
      return "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
    case 403:
      return error.message || "Chỉ Admin sở hữu khu trọ mới được thay đổi phân công."
    case 404:
      return error.message || "Không tìm thấy khu trọ hoặc tài khoản Manager."
    case 409:
      return error.message || "Thao tác phân công bị chặn do xung đột nghiệp vụ."
    case 500:
      return "Máy chủ đang gặp lỗi khi cập nhật phân công. Vui lòng thử lại sau."
    default:
      return error.message
  }
}
