import { Navigate, useLocation } from "react-router"
import { routes } from "@/constants/routes"

// Old return URLs must verify the server receipt just like the current result page.
export default function PaymentSuccessPage() {
  const location = useLocation()
  return <Navigate replace to={`${routes.paymentResult}${location.search}`} />
}
