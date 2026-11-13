import { lazy, Suspense, type ReactNode } from "react"

import { createBrowserRouter, Outlet } from "react-router"

import {
  ProtectedRoute,
  GuestOnlyRoute,
} from "@/components/auth/ProtectedRoute"

import type { AppRole } from "@/types/user"

import { routes } from "@/constants/routes"

import { CustomerNotificationsProvider } from "@/auth/CustomerNotificationsContext"

// The landing page stays in the main bundle; every other route is code-split (C6) so that
// visitors of the home page do not download the management and customer screens.
import HomePage from "@/pages/HomePage/HomePage"

const AvailableRoomsPage = lazy(() => import("@/pages/AvailableRoomsPage/AvailableRoomsPage"))
const MyBillsPage = lazy(() => import("@/pages/MyBillsPage/MyBillsPage"))
const MyBookingsPage = lazy(() => import("@/pages/MyBookingsPage/MyBookingsPage"))
const MyContractsPage = lazy(() => import("@/pages/MyContractsPage/MyContractsPage"))
const MyRoomPage = lazy(() => import("@/pages/MyRoomPage/MyRoomPage"))
const NotificationsPage = lazy(() => import("@/pages/NotificationsPage/NotificationsPage"))
const ProfilePage = lazy(() => import("@/pages/ProfilePage/ProfilePage"))
const ManagementBookingsPage = lazy(() => import("@/pages/ManagementBookingsPage/ManagementBookingsPage"))
const ManagementContractsPage = lazy(() => import("@/pages/ManagementContractsPage/ManagementContractsPage"))
const CreateContractPage = lazy(() => import("@/pages/CreateContractPage/CreateContractPage"))
const ManagementDashboardPage = lazy(() => import("@/pages/ManagementDashboardPage/ManagementDashboardPage"))
const ManagementEquipmentPage = lazy(() => import("@/pages/ManagementEquipmentPage/ManagementEquipmentPage"))
const ManagementInvoicesPage = lazy(() => import("@/pages/ManagementInvoicesPage/ManagementInvoicesPage"))
const ManagementMaintenancePage = lazy(() => import("@/pages/ManagementMaintenancePage/ManagementMaintenancePage"))
const ManagementManagersPage = lazy(() => import("@/pages/ManagementManagersPage/ManagementManagersPage"))
const ManagementPaymentReviewsPage = lazy(() => import("@/pages/ManagementPaymentReviewsPage/ManagementPaymentReviewsPage"))
const ManagementPropertiesPage = lazy(() => import("@/pages/ManagementPropertiesPage/ManagementPropertiesPage"))
const ManagementReportsPage = lazy(() => import("@/pages/ManagementReportsPage/ManagementReportsPage"))
const ManagementRefundsPage = lazy(() => import("@/pages/ManagementRefundsPage/ManagementRefundsPage"))
const ManagementRoomsPage = lazy(() => import("@/pages/ManagementRoomsPage/ManagementRoomsPage"))
const ManagementUsersPage = lazy(() => import("@/pages/ManagementUsersPage/ManagementUsersPage"))
const ManagementViewingsPage = lazy(() => import("@/pages/ManagementViewingsPage/ManagementViewingsPage"))
const LoginPage = lazy(() => import("@/pages/LoginPage/LoginPage"))
const PropertyDetailPage = lazy(() => import("@/pages/PropertyDetailPage/PropertyDetailPage"))
const RegisterPage = lazy(() => import("@/pages/RegisterPage/RegisterPage"))
const DepositPage = lazy(() => import("@/pages/DepositPage/DepositPage"))
const ViewingRequestPage = lazy(() => import("@/pages/ViewingRequestPage/ViewingRequestPage"))
const PaymentSuccessPage = lazy(() => import("@/pages/PaymentSuccessPage/PaymentSuccessPage"))
const InvoicePaymentResultPage = lazy(() => import("@/pages/InvoicePaymentResultPage/InvoicePaymentResultPage"))

const managementRoles: readonly AppRole[] = ["ADMIN", "MANAGER"]
const adminOnly: readonly AppRole[] = ["ADMIN"]
const managerOnly: readonly AppRole[] = ["MANAGER"]
const customerOnly: readonly AppRole[] = ["CUSTOMER"]

function protectedElement(element: ReactNode, roles?: readonly AppRole[]) {
  return <ProtectedRoute roles={roles}>{element}</ProtectedRoute>
}

function RouteFallback() {
  return (
    <div role="status" aria-live="polite" className="flex min-h-[50vh] items-center justify-center text-sm text-slate-500">
      Đang tải...
    </div>
  )
}

function AppLayout() {
  return (
    <CustomerNotificationsProvider>
      <Suspense fallback={<RouteFallback />}>
        <Outlet />
      </Suspense>
    </CustomerNotificationsProvider>
  )
}

function guestElement(element: ReactNode) {
  return <GuestOnlyRoute>{element}</GuestOnlyRoute>
}


export const router = createBrowserRouter([
  {
    Component: AppLayout,
    children: [
      { path: routes.home, Component: HomePage },

      { path: routes.propertyDetail, Component: PropertyDetailPage },

      { path: routes.availableRooms, Component: AvailableRoomsPage },

      {
        path: routes.myRoom,
        element: protectedElement(<MyRoomPage />, customerOnly),
      },

      {
        path: routes.myContracts,
        element: protectedElement(<MyContractsPage />, customerOnly),
      },

      {
        path: routes.myBills,
        element: protectedElement(<MyBillsPage />, customerOnly),
      },

      {
        path: routes.myBookings,
        element: protectedElement(<MyBookingsPage />, customerOnly),
      },

      {
        path: routes.notifications,
        element: protectedElement(<NotificationsPage />),
      },

      { path: routes.profile, element: protectedElement(<ProfilePage />) },

      {
        path: routes.managementDashboard,
        element: protectedElement(<ManagementDashboardPage />, managementRoles),
      },

      {
        path: routes.managementProperties,
        element: protectedElement(
          <ManagementPropertiesPage />,
          managementRoles,
        ),
      },

      {
        path: routes.managementPropertyRooms,
        element: protectedElement(<ManagementRoomsPage />, managementRoles),
      },

      {
        path: routes.managementRooms,
        element: protectedElement(<ManagementRoomsPage />, managementRoles),
      },

      {
        path: routes.managementBookings,
        element: protectedElement(<ManagementBookingsPage />, managementRoles),
      },

      {
        path: routes.managementViewings,
        element: protectedElement(<ManagementViewingsPage />, managementRoles),
      },

      {
        path: routes.managementContracts,
        element: protectedElement(<ManagementContractsPage />, managementRoles),
      },

      {
        path: routes.managementCreateContract,
        element: protectedElement(<CreateContractPage />, managerOnly),
      },
      {
        path: routes.managementInvoices,
        element: protectedElement(<ManagementInvoicesPage />, managementRoles),
      },

      {
        path: routes.managementEquipment,
        element: protectedElement(<ManagementEquipmentPage />, managementRoles),
      },

      {
        path: routes.managementMaintenance,
        element: protectedElement(
          <ManagementMaintenancePage />,
          managementRoles,
        ),
      },

      {
        path: routes.managementRefunds,
        element: protectedElement(<ManagementRefundsPage />, adminOnly),
      },
      {
        path: routes.managementReports,
        element: protectedElement(<ManagementReportsPage />, adminOnly),
      },

      {
        path: routes.managementManagers,
        element: protectedElement(<ManagementManagersPage />, adminOnly),
      },

      {
        path: routes.managementPaymentReviews,
        element: protectedElement(<ManagementPaymentReviewsPage />, adminOnly),
      },

      {
        path: routes.managementUsers,
        element: protectedElement(<ManagementUsersPage />, adminOnly),
      },

      { path: routes.login, element: guestElement(<LoginPage />) },

      { path: routes.register, element: guestElement(<RegisterPage />) },

      {
        path: routes.deposit,
        element: protectedElement(<DepositPage />, customerOnly),
      },

      {
        path: routes.viewingRequest,
        element: protectedElement(<ViewingRequestPage />, customerOnly),
      },

      {
        path: routes.paymentSuccess,
        element: protectedElement(<PaymentSuccessPage />, customerOnly),
      },

      {
        path: routes.paymentResult,
        element: protectedElement(<InvoicePaymentResultPage />, customerOnly),
      },
    ],
  },
])

