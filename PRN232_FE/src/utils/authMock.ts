import { redirect } from "react-router"
import { routes } from "@/constants/routes"
import type { CurrentUser } from "@/types/user"

export type MockAccount = {
  email: string
  password: string
  user: CurrentUser
}

export const mockAccounts: MockAccount[] = [
  {
    email: "ngocanh@example.com",
    password: "customer123",
    user: {
      id: "customer-1",
      name: "Ngọc Anh",
      email: "ngocanh@example.com",
      phone: "0909 123 456",
      role: "CUSTOMER",
    },
  },
  {
    email: "admin@troviet.vn",
    password: "admin123",
    user: {
      id: "admin-1",
      name: "Chủ trọ",
      email: "admin@troviet.vn",
      phone: "0900 000 000",
      role: "ADMIN",
    },
  },
  {
    email: "nguyenvana@example.com",
    password: "manager123",
    user: {
      id: "manager-1",
      name: "Nguyễn Văn A",
      email: "nguyenvana@example.com",
      phone: "0901 234 567",
      role: "MANAGER",
      assignedPropertyIds: ["sunrise-residence", "happy-home"],
    },
  },
]

export function authenticateMockUser(email: string, password: string) {
  return mockAccounts.find(
    (account) =>
      account.email.toLowerCase() === email.trim().toLowerCase() &&
      account.password === password,
  )?.user
}

export function startMockSession(user: CurrentUser) {
  window.localStorage.setItem("troviet-auth", "true")
  if (user.role === "ADMIN" || user.role === "MANAGER") {
    window.localStorage.setItem("troviet-role", user.role)
  } else {
    window.localStorage.removeItem("troviet-role")
  }
}

export function endMockSession() {
  window.localStorage.removeItem("troviet-auth")
  window.localStorage.removeItem("troviet-role")
}

export function requireMockAuth() {
  if (window.localStorage.getItem("troviet-auth") !== "true") {
    throw redirect(routes.login)
  }

  return null
}
