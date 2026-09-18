import { useEffect, useState } from "react"
import { useLocation } from "react-router"
import Footer from "@/components/common/Footer"
import Navbar from "@/components/common/Navbar"
import HeroSearch from "@/components/home/HeroSearch"
import MapSection from "@/components/home/MapSection"
import SearchSection from "@/components/home/SearchSection"
import { properties } from "@/data/properties"
import { scrollToHash } from "@/utils/scrollToHash"

export default function HomePage() {
  const location = useLocation()
  const [selected, setSelected] = useState(properties[0])

  useEffect(() => {
    if (!["#gioi-thieu", "#tim-phong", "#khu-vuc"].includes(location.hash))
      return
    const frame = window.requestAnimationFrame(() =>
      scrollToHash(location.hash),
    )
    return () => window.cancelAnimationFrame(frame)
  }, [location.hash])

  return (
    <main className="min-h-screen bg-[#fcfdfd] text-slate-800">
      <Navbar />
      <HeroSearch />
      <SearchSection
        properties={properties}
        onMap={(property) => {
          setSelected(property)
          document
            .getElementById("khu-vuc")
            ?.scrollIntoView({ behavior: "smooth" })
        }}
      />
      <MapSection selected={selected} onSelect={setSelected} />
      <Footer />
    </main>
  )
}
