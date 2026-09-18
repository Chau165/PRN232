import type { ReactNode } from "react"
import PortalDrawer from "@/components/customer/PortalDrawer"

type ManagementDetailDrawerProps = {
  title: string
  eyebrow?: string
  onClose: () => void
  children: ReactNode
}

export default function ManagementDetailDrawer({
  title,
  eyebrow,
  onClose,
  children,
}: ManagementDetailDrawerProps) {
  return (
    <PortalDrawer title={title} eyebrow={eyebrow} onClose={onClose}>
      {children}
    </PortalDrawer>
  )
}
