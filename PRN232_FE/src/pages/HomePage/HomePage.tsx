import { useEffect, useMemo } from "react"

import { useLocation, useNavigate } from "react-router"

import { publicApi } from "@/api/publicBrowsingApi"

import Footer from "@/components/common/Footer"

import Navbar from "@/components/common/Navbar"

import HeroSearch from "@/components/home/HeroSearch"

import MapSection from "@/components/home/MapSection"

import SearchSection from "@/components/home/SearchSection"

import { usePublicApiResource } from "@/hooks/usePublicApiResource"

import {
  propertyQueryFromSearch,
  propertySearchParams,
  type PropertySearchDraft,
} from "@/utils/publicQueryState"

import { scrollToHash } from "@/utils/scrollToHash"

export default function HomePage() {
  const location = useLocation()

  const navigate = useNavigate()

  const query = useMemo(
    () => propertyQueryFromSearch(location.search),
    [location.search],
  )

  const resource = usePublicApiResource(location.search, (signal) =>
    publicApi.listProperties(query, signal),
  )

  function updateSearch(params: URLSearchParams) {
    const search = params.toString()

    navigate({
      pathname: location.pathname,
      search: search ? `?${search}` : "",
      hash: location.hash,
    })
  }

  function applySearch(draft: PropertySearchDraft) {
    updateSearch(propertySearchParams(draft))
  }

  function changePage(page: number) {
    const params = new URLSearchParams(location.search)

    params.set("page", String(page))

    updateSearch(params)
  }

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
        resource={resource}
        query={query}
        onSearch={applySearch}
        onPageChange={changePage}
      />
      <MapSection />
      <Footer />
    </main>
  )
}
