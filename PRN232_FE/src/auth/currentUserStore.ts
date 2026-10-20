import type { CurrentUser } from "@/types/user"

let currentUser: CurrentUser | null = null

export function setCurrentUserSnapshot(user: CurrentUser | null) {
  currentUser = user
}

export function getCurrentUserSnapshot() {
  return currentUser
}
