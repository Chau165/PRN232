import { apiRequest } from "@/api/apiClient"
import type {
  AuthResponseDto,
  LoginRequestDto,
  RegisterRequestDto,
} from "@/types/auth"

export function login(request: LoginRequestDto) {
  return apiRequest<AuthResponseDto>("/auth/login", {
    method: "POST",
    json: request,
  })
}

export function register(request: RegisterRequestDto) {
  return apiRequest<AuthResponseDto>("/auth/register", {
    method: "POST",
    json: request,
  })
}
