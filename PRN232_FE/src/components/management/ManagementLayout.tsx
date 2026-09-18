import { useState } from "react"
import { useLocation } from "react-router"
import ManagementHeader from "@/components/management/ManagementHeader"
import ManagementSidebar from "@/components/management/ManagementSidebar"
import type { CurrentUser } from "@/types/user"

type ManagementLayoutProps = {
  user: CurrentUser
  title: string
  children: React.ReactNode
}

export default function ManagementLayout({
  user,
  title,
  children,
}: ManagementLayoutProps) {
  const [menuOpen, setMenuOpen] = useState(false)
  const location = useLocation()
  return (
    <div className="min-h-screen bg-[#f8fbfb]">
      <div className="flex min-h-screen">
        <ManagementSidebar
          user={user}
          pathname={location.pathname}
          open={menuOpen}
          onClose={() => setMenuOpen(false)}
        />
        <div className="min-w-0 flex-1">
          <ManagementHeader
            user={user}
            title={title}
            onMenu={() => setMenuOpen(true)}
          />
          <main className="mx-auto max-w-[1440px] px-5 py-7 lg:px-8">
            {children}
          </main>
        </div>
      </div>
    </div>
  )
}
