import { fireEvent, render, screen } from "@testing-library/react"
import { beforeEach, expect, it, vi } from "vitest"
import type { ReactNode } from "react"
import PropertyLocationMap from "@/components/management/PropertyLocationMap"
const state = vi.hoisted(() => ({ panTo: vi.fn(), setView: vi.fn(), fitBounds: vi.fn(), events: {} as { click?: (event: {latlng:{lat:number;lng:number}}) => void } }))
vi.mock("leaflet",()=>({default:{divIcon:()=>({})}}))
vi.mock("react-leaflet",()=>({
  MapContainer:({children}:{children:ReactNode})=><div>{children}</div>,TileLayer:()=>null,
  useMap:()=>({panTo:state.panTo,setView:state.setView,fitBounds:state.fitBounds}),useMapEvents:(events:typeof state.events)=>{state.events=events;return {}},
  Marker:({position,draggable,eventHandlers}:{position:number[];draggable:boolean;eventHandlers:{dragend:(e:unknown)=>void}})=><button disabled={!draggable} data-position={position.join(",")} onClick={()=>eventHandlers.dragend({target:{getLatLng:()=>({lat:10.86,lng:106.78})}})}>Ghim bản đồ</button>
}))
beforeEach(()=>{state.panTo.mockReset();state.setView.mockReset();state.fitBounds.mockReset()})
it("forwards map clicks and dragend with latitude/longitude in the right order",()=>{
  const pick=vi.fn();const view=render(<PropertyLocationMap point={null} onPick={pick}/>)
  state.events.click?.({latlng:{lat:10.85,lng:106.77}})
  expect(pick).toHaveBeenLastCalledWith({latitude:10.85,longitude:106.77})
  view.rerender(<PropertyLocationMap point={{latitude:10.85,longitude:106.77}} onPick={pick}/>)
  expect(state.panTo).toHaveBeenLastCalledWith([10.85,106.77])
  fireEvent.click(screen.getByRole("button",{name:"Ghim bản đồ"}))
  expect(pick).toHaveBeenLastCalledWith({latitude:10.86,longitude:106.78})
})
it("renders the saved marker and locks click/drag while saving",()=>{
  const pick=vi.fn();render(<PropertyLocationMap point={{latitude:21.032,longitude:105.814}} onPick={pick} disabled/>)
  const marker=screen.getByRole("button",{name:"Ghim bản đồ"}) as HTMLButtonElement
  expect(marker.dataset.position).toBe("21.032,105.814");expect(marker.disabled).toBe(true)
  state.events.click?.({latlng:{lat:10.85,lng:106.77}});fireEvent.click(marker)
  expect(pick).not.toHaveBeenCalled()
})
it("moves the view to the selected area, using its bounds when known",()=>{
  const pick=vi.fn();const view=render(<PropertyLocationMap point={null} focus={{latitude:10.88,longitude:106.83,zoom:15}} onPick={pick}/>)
  expect(state.setView).toHaveBeenLastCalledWith([10.88,106.83],15)
  view.rerender(<PropertyLocationMap point={null} focus={{latitude:10.88,longitude:106.83,zoom:15,bounds:[[10.86,106.8],[10.9,106.86]]}} onPick={pick}/>)
  expect(state.fitBounds).toHaveBeenLastCalledWith([[10.86,106.8],[10.9,106.86]],{maxZoom:15})
  expect(pick).not.toHaveBeenCalled()
})
