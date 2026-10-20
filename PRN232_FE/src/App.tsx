import { RouterProvider } from "react-router"
import { AuthProvider } from "@/auth/AuthContext"
import { router } from "@/app/router"

export default function App() {
  return (
    <AuthProvider>
      <RouterProvider router={router} />
    </AuthProvider>
  )
}
