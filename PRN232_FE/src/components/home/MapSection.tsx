import { lazy, Suspense, useEffect, useRef, useState } from "react"

import { publicApi } from "@/services/publicBrowsingService"
import type { PublicPropertyAreaCard } from "@/types/publicBrowsing"
import {
  AreaDiscoveryError,
  discoverCurrentArea,
  type AreaCoordinates,
  type AreaDiscoveryResult,
  type AreaDiscoveryStep,
} from "@/utils/areaDiscovery"

const AreaPropertyMap = lazy(() => import("@/components/home/AreaPropertyMap"))

const DEFAULT_MAP_CENTER: AreaCoordinates = {
  latitude: 10.8231,
  longitude: 106.6297,
}

type MapSectionState =
  | { status: "idle" }
  | { status: "loading"; step: AreaDiscoveryStep }
  | { status: "error"; error: AreaDiscoveryError }
  | { status: "success"; result: AreaDiscoveryResult }
  | { status: "empty"; result: AreaDiscoveryResult }

function areaName(result: AreaDiscoveryResult): string {
  const { area, matchedLevel } = result
  if (matchedLevel === "district" && area.district) return area.district
  return area.ward ?? area.district ?? "khu vực hiện tại"
}

function hasCoordinates(property: PublicPropertyAreaCard): boolean {
  return (
    Number.isFinite(property.latitude) &&
    Number.isFinite(property.longitude) &&
    property.latitude >= -90 &&
    property.latitude <= 90 &&
    property.longitude >= -180 &&
    property.longitude <= 180 &&
    (property.latitude !== 0 || property.longitude !== 0)
  )
}

export default function MapSection() {
  const [state, setState] = useState<MapSectionState>({ status: "idle" })
  const [selectedPropertyId, setSelectedPropertyId] = useState<number | null>(null)
  const [userLocation, setUserLocation] = useState<AreaCoordinates | null>(null)
  const requestId = useRef(0)
  const activeRequest = useRef<AbortController | null>(null)

  useEffect(
    () => () => {
      requestId.current += 1
      activeRequest.current?.abort()
    },
    [],
  )

  async function discoverArea() {
    activeRequest.current?.abort()
    const controller = new AbortController()
    activeRequest.current = controller
    const currentRequest = ++requestId.current
    setSelectedPropertyId(null)
    setUserLocation(null)
    setState({ status: "loading", step: "location" })

    try {
      const result = await discoverCurrentArea({
        geolocation: navigator.geolocation ?? null,
        fetchImplementation: fetch,
        listPropertiesByArea: (query, signal) =>
          publicApi.listPropertiesByArea(query, signal),
        signal: controller.signal,
        onProgress: (step) => {
          if (requestId.current === currentRequest) {
            setState({ status: "loading", step })
          }
        },
        onLocation: (coordinates) => {
          if (requestId.current === currentRequest) setUserLocation(coordinates)
        },
      })

      if (requestId.current !== currentRequest) return
      setState({
        status: result.properties.length > 0 ? "success" : "empty",
        result,
      })
    } catch (error) {
      if (controller.signal.aborted || requestId.current !== currentRequest) return
      setState({
        status: "error",
        error:
          error instanceof AreaDiscoveryError
            ? error
            : new AreaDiscoveryError(
                "area-api",
                "Không tải được danh sách khu trọ. Vui lòng thử lại.",
              ),
      })
    } finally {
      if (requestId.current === currentRequest) activeRequest.current = null
    }
  }

  const isLoading = state.status === "loading"
  const result = state.status === "success" || state.status === "empty"
    ? state.result
    : null
  const properties = result?.properties ?? []
  const unlocatedProperties = properties.filter((property) => !hasCoordinates(property)).length

  return (
    <section id="khu-vuc" className="scroll-mt-24">
      <div className="mx-auto max-w-[1180px] px-6 pb-20 pt-4 lg:pt-8">
        <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <p className="text-[11px] font-bold uppercase tracking-[.16em] text-[#087775]">
              Khám phá khu vực
            </p>
            <h2 className="mt-1 font-display text-[27px] font-bold tracking-[-.04em] text-slate-900">
              Khu trọ quanh bạn
            </h2>
            <p className="mt-2 max-w-2xl text-[13px] leading-6 text-slate-500">
              Bản đồ luôn sẵn sàng. Cấp quyền vị trí để xem khu trọ trong cùng phường hoặc quận.
            </p>
          </div>
          <button
            type="button"
            onClick={discoverArea}
            disabled={isLoading}
            className="inline-flex min-h-11 shrink-0 items-center justify-center gap-2 rounded-xl bg-[#087775] px-5 py-3 text-sm font-bold text-white transition hover:bg-[#066a68] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-[#087775] disabled:cursor-wait disabled:opacity-60"
          >
            {isLoading ? "Đang xác định khu vực..." : "Tìm khu trọ quanh tôi"}
          </button>
        </div>

        <Suspense
          fallback={
            <div
              role="img"
              aria-label="Bản đồ đang tải"
              className="grid h-[390px] place-items-center rounded-2xl border border-slate-200 bg-slate-50 text-sm text-slate-500 sm:h-[470px]"
            >
              Đang tải bản đồ...
            </div>
          }
        >
          <AreaPropertyMap
            properties={properties}
            center={userLocation ?? DEFAULT_MAP_CENTER}
            userLocation={userLocation}
            selectedPropertyId={selectedPropertyId}
            onSelect={setSelectedPropertyId}
          />
        </Suspense>

        {isLoading && (
          <p role="status" className="mt-4 rounded-xl bg-white px-5 py-4 text-center text-sm text-slate-600">
            {state.step === "location" && "Đang xin quyền và lấy vị trí hiện tại..."}
            {state.step === "reverse-geocode" && "Đang xác định phường, quận và thành phố..."}
            {state.step === "properties" && "Đang tải khu trọ trong khu vực..."}
          </p>
        )}

        {state.status === "error" && (
          <div role="alert" className="mt-4 rounded-xl border border-rose-100 bg-white px-5 py-5 text-sm text-rose-700">
            <p>{state.error.message}</p>
            <button
              type="button"
              onClick={discoverArea}
              className="mt-3 rounded-lg bg-[#087775] px-4 py-2 text-xs font-bold text-white hover:bg-[#066a68]"
            >
              Thử lại
            </button>
          </div>
        )}

        {state.status === "idle" && (
          <p className="mt-4 rounded-xl bg-white px-5 py-4 text-center text-sm text-slate-600">
            Cho phép vị trí để chuyển bản đồ tới bạn và tìm khu trọ cùng khu vực. Tọa độ chỉ dùng để xác định khu vực, không được ứng dụng lưu lại.
          </p>
        )}

        {result && (
          <div className="mt-4 space-y-3">
            <div className="flex flex-wrap items-baseline justify-between gap-2">
              <h3 className="font-bold text-slate-900">Khu trọ tại {areaName(result)}</h3>
              <span className="text-xs text-slate-500">
                {result.properties.length} khu trọ có phòng đang cho thuê
              </span>
            </div>
            {state.status === "empty" && (
              <p className="rounded-xl bg-white px-5 py-6 text-center text-sm text-slate-600">
                Chưa có khu trọ đang cho thuê tại {areaName(result)}.
              </p>
            )}
            {unlocatedProperties > 0 && (
              <p role="status" className="text-xs text-amber-700">
                {unlocatedProperties} khu trọ chưa có tọa độ hợp lệ nên không thể đặt marker trên bản đồ.
              </p>
            )}
          </div>
        )}
      </div>
    </section>
  )
}
