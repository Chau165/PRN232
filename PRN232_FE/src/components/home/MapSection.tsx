import { lazy, Suspense, useEffect, useRef, useState } from "react"

import { publicApi } from "@/services/publicBrowsingService"
import type { PublicPropertyAreaCard } from "@/types/publicBrowsing"
import {
  AreaDiscoveryError,
  discoverCurrentArea,
  discoverEnteredArea,
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
  return area.ward ?? area.district ?? area.city ?? "khu vực hiện tại"
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
  const [locationInput, setLocationInput] = useState("")

  const [useCurrentLocation, setUseCurrentLocation] = useState(false)

  const [showLocationOption, setShowLocationOption] = useState(false)

  const [state, setState] = useState<MapSectionState>({ status: "idle" })
  const [selectedPropertyId, setSelectedPropertyId] = useState<number | null>(null,
  )
  const [userLocation, setUserLocation] = useState<AreaCoordinates | null>(null)

  const [mapCenter, setMapCenter] = useState(DEFAULT_MAP_CENTER)
  const requestId = useRef(0)
  const activeRequest = useRef<AbortController | null>(null)

  useEffect(
    () => () => {
      requestId.current += 1
      activeRequest.current?.abort()
    },
    [],
  )

  async function discoverArea(currentLocation = useCurrentLocation) {
    activeRequest.current?.abort()
    const controller = new AbortController()
    activeRequest.current = controller
    const currentRequest = ++requestId.current
    setSelectedPropertyId(null)
    setUserLocation(null)

    setShowLocationOption(false)

    setState({ status: "loading", step: currentLocation ? "location" : "geocode",
    })

    try {
      const options = {
        fetchImplementation: fetch,
        listPropertiesByArea: (query: Parameters<typeof publicApi.listPropertiesByArea>[0],
          signal?: AbortSignal,
        ) =>
          publicApi.listPropertiesByArea(query, signal),
        signal: controller.signal,
        onProgress: (step: AreaDiscoveryStep) => {
          if (requestId.current === currentRequest) {
            setState({ status: "loading", step })
          }
        },
        onLocation: (coordinates: AreaCoordinates) => {
          if (requestId.current === currentRequest) {
            setMapCenter(coordinates)

            if (currentLocation) setUserLocation(coordinates)
          }
        },
      }

      const result = currentLocation
        ? await discoverCurrentArea({
            ...options,
            geolocation: navigator.geolocation ?? null,
          })
        : await discoverEnteredArea(locationInput, options)

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
  const unlocatedProperties = properties.filter((property) => !hasCoordinates(property),
  ).length

  return (
    <section id="khu-vuc" className="scroll-mt-24">
      <div className="public-container pb-20 pt-4 lg:pt-8">
        <div className="mb-6 flex flex-col gap-4 sm:flex-row sm:items-end sm:justify-between">
          <div>
            <h2 className="font-display text-[27px] font-bold tracking-[-.04em] text-slate-900">
              Tìm khu trọ theo địa điểm
            </h2>
            <p className="mt-2 max-w-2xl text-[13px] leading-6 text-slate-500">
              Nhập địa chỉ, tên đường hoặc khu vực bạn muốn ở, hoặc chọn vị trí
              hiện tại của bạn.
            </p>
          </div>
        </div>

        <form
          onSubmit={(event) => {
            event.preventDefault()
            void discoverArea()
          }}
          className="relative z-[1001] mb-5 rounded-xl border border-slate-200 bg-white p-3 sm:flex sm:items-end sm:gap-3"
        >
          <div
            className="relative min-w-0 flex-1"
            onBlur={(event) => {
              if (!event.currentTarget.contains(event.relatedTarget))
                setShowLocationOption(false)
            }}
          >
            <label
              htmlFor="map-location"
              className="mb-2 block text-xs font-semibold text-slate-600"
            >
              Bạn muốn tìm khu trọ ở đâu?
            </label>
            <input
              id="map-location"
              type="search"
              autoComplete="off"
              value={locationInput}
              onFocus={() => setShowLocationOption(true)}
              onKeyDown={(event) => {
                if (event.key === "Escape") setShowLocationOption(false)
              }}
              onChange={(event) => {
                setLocationInput(event.target.value)
                setUseCurrentLocation(false)
                setShowLocationOption(true)
              }}
              placeholder="Ví dụ: Gò Vấp, TP. Hồ Chí Minh"
              aria-describedby="map-location-help"
              aria-controls={
                showLocationOption ? "map-location-options" : undefined
              }
              className="h-12 w-full rounded-lg border border-slate-200 px-4 text-sm text-slate-800 outline-none focus:border-teal-600 focus:ring-2 focus:ring-teal-100"
            />
            {showLocationOption && (
              <div
                id="map-location-options"
                className="absolute left-0 right-0 top-full mt-2 rounded-lg border border-slate-200 bg-white p-1 shadow-lg"
              >
          <button
            type="button"
            onClick={() => {
                    setUseCurrentLocation(true)

                    setLocationInput("Theo vị trí của bạn hiện tại")

                    void discoverArea(true)
                  }}
                  className="w-full rounded-md px-4 py-3 text-left text-sm font-semibold text-[#087775] hover:bg-teal-50 focus-visible:bg-teal-50"
                >
                  Theo vị trí của bạn hiện tại
                </button>
              </div>
            )}
          </div>
          <button
            type="submit"
            disabled={isLoading}
            className="mt-3 flex h-12 w-full shrink-0 items-center justify-center rounded-lg bg-[#087775] px-6 text-sm font-bold text-white hover:bg-[#066a68] focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-teal-700 disabled:cursor-wait disabled:opacity-60 sm:mt-0 sm:w-auto"
          >
            {isLoading ? "Đang tìm khu trọ..." : "Tìm trên bản đồ"}
          </button>
        </form>
        <p id="map-location-help" className="mb-4 text-xs text-slate-500">
          Kết quả được lọc theo phường, quận hoặc thành phố của địa điểm tìm
          kiếm.
        </p>

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
            searchLocationLabel={result?.locationLabel}
            properties={properties}
            center={mapCenter}
            userLocation={userLocation}
            selectedPropertyId={selectedPropertyId}
            onSelect={setSelectedPropertyId}
          />
        </Suspense>

        {isLoading && (
          <p role="status" className="mt-4 rounded-xl bg-white px-5 py-4 text-center text-sm text-slate-600">
            {state.step === "location" && "Đang xin quyền và lấy vị trí hiện tại..."}
            {state.step === "geocode" && "Đang tìm địa điểm bạn nhập..."}
            {state.step === "reverse-geocode" && "Đang xác định phường, quận và thành phố..."}
            {state.step === "properties" && "Đang tải khu trọ trong khu vực..."}
          </p>
        )}

        {state.status === "error" && (
          <div role="alert" className="mt-4 rounded-xl border border-rose-100 bg-white px-5 py-5 text-sm text-rose-700">
            <p>{state.error.message}</p>
            <button
              type="button"
              onClick={() => void discoverArea()}
              className="mt-3 rounded-lg bg-[#087775] px-4 py-2 text-xs font-bold text-white hover:bg-[#066a68]"
            >
              Thử lại
            </button>
          </div>
        )}

        {state.status === "idle" && (
          <p className="mt-4 rounded-xl bg-white px-5 py-4 text-center text-sm text-slate-600">
            Nhập địa điểm để bắt đầu, hoặc bấm vào ô tìm kiếm và chọn “Theo vị
            trí của bạn hiện tại”.
          </p>
        )}

        {result && (
          <div className="mt-4 space-y-3">
            {result.locationLabel && (
              <p className="text-sm text-slate-600">
                Địa điểm tìm kiếm: {result.locationLabel}
              </p>
            )}
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
