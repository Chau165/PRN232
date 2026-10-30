import { PublicApiError } from "@/services/publicBrowsingService"

export function publicApiErrorMessage(error: PublicApiError): string {
  if (error.kind === "network")
    return "Không thể kết nối máy chủ. Kiểm tra kết nối rồi thử lại."
  if (error.kind === "server")
    return "Máy chủ đang gặp sự cố. Vui lòng thử lại sau."
  if (error.kind === "not-found")
    return "Không tìm thấy dữ liệu công khai phù hợp."
  if (error.kind === "validation")
    return "Máy chủ từ chối điều kiện tìm kiếm. Hãy kiểm tra bộ lọc."
  return "Không thể tải dữ liệu lúc này. Vui lòng thử lại."
}

export function publicApiValidationMessages(error: PublicApiError): string[] {
  return Object.values(error.validationErrors ?? {}).flat()
}
