import { createBrowserRouter } from "react-router"
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
import PaymentSuccessPage from "@/pages/PaymentSuccessPage/PaymentSuccessPage"
import { routes } from "@/constants/routes"
import { requireMockAuth } from "@/utils/authMock"
import {
  requireManagementPermission,
  requireManagementRole,
} from "@/utils/managementAuth"

export const router = createBrowserRouter([
  { path: routes.home, Component: HomePage },
  { path: routes.propertyDetail, Component: PropertyDetailPage },
  { path: routes.availableRooms, Component: AvailableRoomsPage },
  { path: routes.myRoom, Component: MyRoomPage, loader: requireMockAuth },
  {
    path: routes.myContracts,
    Component: MyContractsPage,
    loader: requireMockAuth,
  },
  { path: routes.myBills, Component: MyBillsPage, loader: requireMockAuth },
  {
    path: routes.myBookings,
    Component: MyBookingsPage,
    loader: requireMockAuth,
  },
  {
    path: routes.notifications,
    Component: NotificationsPage,
    loader: requireMockAuth,
  },
  { path: routes.profile, Component: ProfilePage, loader: requireMockAuth },
  {
    path: routes.managementDashboard,
    Component: ManagementDashboardPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementAreas,
    Component: ManagementAreasPage,
    loader: requireManagementPermission("viewAreas"),
  },
  {
    path: routes.managementProperties,
    Component: ManagementPropertiesPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementRooms,
    Component: ManagementRoomsPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementTenants,
    Component: ManagementTenantsPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementBookings,
    Component: ManagementBookingsPage,
    loader: requireManagementRole,
  },
  { path: routes.managementViewings, Component: ManagementViewingsPage, loader: requireManagementRole },
  {
    path: routes.managementContracts,
    Component: ManagementContractsPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementCreateContract,
    Component: CreateContractPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementInvoices,
    Component: ManagementInvoicesPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementEquipment,
    Component: ManagementEquipmentPage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementMaintenance,
    Component: ManagementMaintenancePage,
    loader: requireManagementRole,
  },
  {
    path: routes.managementReports,
    Component: ManagementReportsPage,
    loader: requireManagementPermission("viewReports"),
  },
  {
    path: routes.managementManagers,
    Component: ManagementManagersPage,
    loader: requireManagementPermission("manageManagers"),
  },
  {
    path: routes.managementUsers,
    Component: ManagementUsersPage,
    loader: requireManagementPermission("manageUsers"),
  },
  { path: routes.login, Component: LoginPage },
  { path: routes.register, Component: RegisterPage },
  { path: routes.deposit, Component: DepositPage, loader: requireMockAuth },
  { path: routes.paymentSuccess, Component: PaymentSuccessPage, loader: requireMockAuth },
])
