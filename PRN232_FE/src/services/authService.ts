import { apiRequest } from "@/api/apiClient"
import type {
  AuthResponseDto,
  GoogleLoginRequestDto,
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

/** Exchanges a Google ID token for the app's own session (same response as login). */
export function loginWithGoogle(request: GoogleLoginRequestDto) {
  return apiRequest<AuthResponseDto>("/auth/google", {
    method: "POST",
    json: request,
  })
}
