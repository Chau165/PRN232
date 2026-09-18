import type { ReactNode } from "react"

export type IconName = "pin" | "gps" | "search" | "chevron" | "arrow" | "wifi" | "snow" | "car" | "camera" | "building" | "sliders" | "check" | "lock" | "washer" | "toilet" | "lift" | "home" | "user" | "calendar" | "file" | "receipt" | "tool" | "wrench" | "chart" | "users"

type IconProps = {
  name: IconName
  size?: number
}

const paths: Record<IconName, ReactNode> = {
  pin: (
    <>
      <path d="M12 21s7-5.15 7-12a7 7 0 1 0-14 0c0 6.85 7 12 7 12Z" />
      <circle cx="12" cy="9" r="2.35" />
    </>
  ),
  gps: (
    <>
      <circle cx="12" cy="12" r="3" />
      <path d="M12 2v3M12 19v3M2 12h3M19 12h3" />
    </>
  ),
  search: (
    <>
      <circle cx="10.8" cy="10.8" r="6.6" />
      <path d="m16 16 4.3 4.3" />
    </>
  ),
  chevron: <path d="m7 10 5 5 5-5" />,
  arrow: <path d="M5 12h14m-6-6 6 6-6 6" />,
  wifi: (
    <>
      <path d="M2.5 8.5a14.1 14.1 0 0 1 19 0M5.5 12a9.7 9.7 0 0 1 13 0M8.9 15.3a4.7 4.7 0 0 1 6.2 0" />
      <circle cx="12" cy="19" r=".7" fill="currentColor" stroke="none" />
    </>
  ),
  snow: (
    <>
      <path d="M12 2v20M4.8 6.2l14.4 11.6M4.8 17.8 19.2 6.2M2 12h20" />
      <path d="m8.6 3.6 3.4 2.2 3.4-2.2M8.6 20.4l3.4-2.2 3.4 2.2" />
    </>
  ),
  car: (
    <>
      <path d="m5 16-1 3h16l-1-3M5 16l1.5-6h11L19 16M5 16h14" />
      <circle cx="7.5" cy="19" r="1" />
      <circle cx="16.5" cy="19" r="1" />
    </>
  ),
  camera: (
    <>
      <path d="M4 7h3l1.3-2h7.4L17 7h3v12H4Z" />
      <circle cx="12" cy="13" r="3.3" />
    </>
  ),
  building: <path d="M4 21V4h12v17M16 9h4v12M8 8h4M8 12h4M8 16h4" />,
  sliders: (
    <>
      <path d="M4 7h16M4 17h16" />
      <circle cx="9" cy="7" r="2" />
      <circle cx="15" cy="17" r="2" />
    </>
  ),
  check: <path d="m5 12 4 4L19 6" />,
  lock: (
    <>
      <rect x="5" y="10" width="14" height="10" rx="2" />
      <path d="M8 10V7a4 4 0 0 1 8 0v3" />
    </>
  ),
  washer: (
    <>
      <rect x="5" y="3" width="14" height="18" rx="2" />
      <circle cx="12" cy="14" r="3.5" />
      <path d="M8 7h.01M11 7h5" />
    </>
  ),
  toilet: (
    <>
      <path d="M6 4h12v5H6zM8 9v6a4 4 0 0 0 8 0V9M6 21h12" />
    </>
  ),
  lift: (
    <>
      <path d="M5 3h14v18H5zM9 11l3-3 3 3M15 13l-3 3-3-3" />
    </>
  ),
  home: <path d="m3 11 9-8 9 8M5 10v10h14V10M9 20v-6h6v6" />,
  user: (
    <>
      <circle cx="9" cy="8" r="3" />
      <path d="M3 20a6 6 0 0 1 12 0M16 5.5a3 3 0 0 1 0 5.8M17 14a5 5 0 0 1 4 6" />
    </>
  ),
  calendar: (
    <>
      <rect x="3" y="5" width="18" height="16" rx="2" />
      <path d="M16 3v4M8 3v4M3 10h18" />
    </>
  ),
  file: (
    <>
      <path d="M6 3h9l3 3v15H6zM15 3v4h4M9 12h6M9 16h6" />
    </>
  ),
  receipt: (
    <>
      <path d="M5 3h14v18l-3-2-4 2-4-2-3 2zM8 8h8M8 12h8M8 16h4" />
    </>
  ),
  tool: <path d="m14.5 6.5 3-3 3 3-3 3M4 20l9.5-9.5M12 4a4 4 0 0 0 5 5" />,
  wrench: (
    <path d="M14.7 6.3a4 4 0 0 0-5.4 5.4L3 18a2 2 0 1 0 2.8 2.8l6.3-6.3a4 4 0 0 0 5.4-5.4L14 12l-2-2z" />
  ),
  chart: <path d="M4 19V5M4 19h17m-14-4 4-4 3 2 5-6" />,
  users: (
    <>
      <circle cx="9" cy="8" r="3" />
      <path d="M3 20a6 6 0 0 1 12 0M16 5.5a3 3 0 0 1 0 5.8M17 14a5 5 0 0 1 4 6" />
    </>
  ),
}

export default function Icon({ name, size = 18 }: IconProps) {
  return (
    <svg
      width={size}
      height={size}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth="1.8"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
    >
      {paths[name]}
    </svg>
  )
}
