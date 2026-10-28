import type { ReactNode } from "react"
import { createBrowserRouter, Outlet } from "react-router"
import { ProtectedRoute, GuestOnlyRoute } from "@/components/auth/ProtectedRoute"
import type { AppRole } from "@/types/user"
import HomePage from "@/pages/HomePage/HomePage"
import AvailableRoomsPage from "@/pages/AvailableRoomsPage/AvailableRoomsPage"
import MyBillsPage from "@/pages/MyBillsPage/MyBillsPage"
import MyBookingsPage from "@/pages/MyBookingsPage/MyBookingsPage"
import MyContractsPage from "@/pages/MyContractsPage/MyContractsPage"
import MyRoomPage from "@/pages/MyRoomPage/MyRoomPage"
import NotificationsPage from "@/pages/NotificationsPage/NotificationsPage"
import ProfilePage from "@/pages/ProfilePage/ProfilePage"
import ManagementAreasPage from "@/pages/ManagementAreasPage/ManagementAreasPage"
import ManagementBookingsPage from "@/pages/ManagementBookingsPage/ManagementBookingsPage"
import ManagementContractsPage from "@/pages/ManagementContractsPage/ManagementContractsPage"
import CreateContractPage from "@/pages/CreateContractPage/CreateContractPage"
import ManagementDashboardPage from "@/pages/ManagementDashboardPage/ManagementDashboardPage"
import ManagementEquipmentPage from "@/pages/ManagementEquipmentPage/ManagementEquipmentPage"
import ManagementInvoicesPage from "@/pages/ManagementInvoicesPage/ManagementInvoicesPage"
import ManagementMaintenancePage from "@/pages/ManagementMaintenancePage/ManagementMaintenancePage"
import ManagementManagersPage from "@/pages/ManagementManagersPage/ManagementManagersPage"
import ManagementPropertiesPage from "@/pages/ManagementPropertiesPage/ManagementPropertiesPage"
import ManagementReportsPage from "@/pages/ManagementReportsPage/ManagementReportsPage"
import ManagementRoomsPage from "@/pages/ManagementRoomsPage/ManagementRoomsPage"
import ManagementTenantsPage from "@/pages/ManagementTenantsPage/ManagementTenantsPage"
import ManagementUsersPage from "@/pages/ManagementUsersPage/ManagementUsersPage"
import ManagementViewingsPage from "@/pages/ManagementViewingsPage/ManagementViewingsPage"
import LoginPage from "@/pages/LoginPage/LoginPage"
import PropertyDetailPage from "@/pages/PropertyDetailPage/PropertyDetailPage"
import RegisterPage from "@/pages/RegisterPage/RegisterPage"
import DepositPage from "@/pages/DepositPage/DepositPage"
import ViewingRequestPage from "@/pages/ViewingRequestPage/ViewingRequestPage"
import PaymentSuccessPage from "@/pages/PaymentSuccessPage/PaymentSuccessPage"
import { routes } from "@/constants/routes"
import { CustomerNotificationsProvider } from "@/auth/CustomerNotificationsContext"

const managementRoles: readonly AppRole[] = ["ADMIN", "MANAGER"]
const adminOnly: readonly AppRole[] = ["ADMIN"]
const customerOnly: readonly AppRole[] = ["CUSTOMER"]

function protectedElement(element: ReactNode, roles?: readonly AppRole[]) {
  return <ProtectedRoute roles={roles}>{element}</ProtectedRoute>
}

function AppLayout() {
  return <CustomerNotificationsProvider><Outlet /></CustomerNotificationsProvider>
}

function guestElement(element: ReactNode) {
  return <GuestOnlyRoute>{element}</GuestOnlyRoute>
}

export const router = createBrowserRouter([{ Component: AppLayout, children: [
  { path: routes.home, Component: HomePage },
  { path: routes.propertyDetail, Component: PropertyDetailPage },
  { path: routes.availableRooms, Component: AvailableRoomsPage },
  { path: routes.myRoom, element: protectedElement(<MyRoomPage />, customerOnly) },
  { path: routes.myContracts, element: protectedElement(<MyContractsPage />, customerOnly) },
  { path: routes.myBills, element: protectedElement(<MyBillsPage />, customerOnly) },
  { path: routes.myBookings, element: protectedElement(<MyBookingsPage />, customerOnly) },
  { path: routes.notifications, element: protectedElement(<NotificationsPage />) },
  { path: routes.profile, element: protectedElement(<ProfilePage />) },
  { path: routes.managementDashboard, element: protectedElement(<ManagementDashboardPage />, managementRoles) },
  { path: routes.managementAreas, element: protectedElement(<ManagementAreasPage />, adminOnly) },
  { path: routes.managementProperties, element: protectedElement(<ManagementPropertiesPage />, managementRoles) },
  { path: routes.managementRooms, element: protectedElement(<ManagementRoomsPage />, managementRoles) },
  { path: routes.managementTenants, element: protectedElement(<ManagementTenantsPage />, managementRoles) },
  { path: routes.managementBookings, element: protectedElement(<ManagementBookingsPage />, managementRoles) },
  { path: routes.managementViewings, element: protectedElement(<ManagementViewingsPage />, managementRoles) },
  { path: routes.managementContracts, element: protectedElement(<ManagementContractsPage />, managementRoles) },
  { path: routes.managementCreateContract, element: protectedElement(<CreateContractPage />, managementRoles) },
  { path: routes.managementInvoices, element: protectedElement(<ManagementInvoicesPage />, managementRoles) },
  { path: routes.managementEquipment, element: protectedElement(<ManagementEquipmentPage />, managementRoles) },
  { path: routes.managementMaintenance, element: protectedElement(<ManagementMaintenancePage />, managementRoles) },
  { path: routes.managementReports, element: protectedElement(<ManagementReportsPage />, adminOnly) },
  { path: routes.managementManagers, element: protectedElement(<ManagementManagersPage />, adminOnly) },
  { path: routes.managementUsers, element: protectedElement(<ManagementUsersPage />, adminOnly) },
  { path: routes.login, element: guestElement(<LoginPage />) },
  { path: routes.register, element: guestElement(<RegisterPage />) },
  { path: routes.deposit, element: protectedElement(<DepositPage />, customerOnly) },
  { path: routes.viewingRequest, element: protectedElement(<ViewingRequestPage />, customerOnly) },
  { path: routes.paymentSuccess, element: protectedElement(<PaymentSuccessPage />, customerOnly) },
]}])
