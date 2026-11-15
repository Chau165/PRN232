import { fireEvent, render, screen, waitFor } from "@testing-library/react"
import { afterEach, expect, it, vi } from "vitest"
import PropertyFormModal from "@/components/management/PropertyFormModal"
import {createProperty,getProperty,updateProperty,type PropertyWriteRequest} from "@/services/propertyService"
import type { PropertyDto } from "@/types/user"
vi.mock("@/components/management/PropertyLocationMap",()=>({default:({point,onPick,disabled}:{point:{latitude:number;longitude:number}|null;onPick:(p:{latitude:number;longitude:number})=>void;disabled:boolean})=><div>
  <output aria-label="Ghim đang hiển thị">{point?point.latitude+","+point.longitude:"Chưa có ghim"}</output>
  <button type="button" disabled={disabled} onClick={()=>onPick({latitude:21.032,longitude:105.814})}>Bấm đặt ghim</button>
  <button type="button" disabled={disabled} onClick={()=>onPick({latitude:21.0334567,longitude:105.8156789})}>Kéo ghim</button>
</div>}))
afterEach(()=>vi.unstubAllGlobals())
async function choose(label:string,text:string){const field=screen.getByRole("combobox",{name:label});fireEvent.focus(field);fireEvent.change(field,{target:{value:text}});fireEvent.click(await screen.findByRole("option",{name:text}))}
function fixture(providerStatus=200){
  const searches: {url:string;init?:RequestInit}[]=[]
  let rejectNextWrite=false
  let stored:PropertyDto|null=null
  const writes:FormData[]=[]
  vi.stubGlobal("fetch",vi.fn(async(input,init?:RequestInit)=>{
    const url=String(input)
    if(new URL(url,"http://localhost").host === "photon.komoot.io"){
      searches.push({url,init})
      return Response.json({features:[{geometry:{type:"Point",coordinates:[105.815,21.033]},properties:{street:"Lê Lợi",housenumber:"18",countrycode:"VN",city:"Hà Nội"}}]}, {status:providerStatus})
    }
    if(url.endsWith("/locations/provinces"))return Response.json([{provinceCode:"01",provinceName:"Thành phố Hà Nội"}])
    if(url.includes("/locations/wards?"))return Response.json([{wardCode:"00004",wardName:"Phường Ba Đình"}])
    if(init?.method==="POST"||init?.method==="PUT"){
      const body=init.body as FormData;writes.push(body)
      if(rejectNextWrite){rejectNextWrite=false;return Response.json({title:"Không thể lưu khu trọ lúc này."},{status:503})}
      stored={propertyId:31,ownerId:9,ownerName:"Admin",roomCount:0,ward:"Phường Ba Đình",city:"Thành phố Hà Nội",provinceCode:"01",wardCode:"00004",propertyName:String(body.get("propertyName")),addressText:String(body.get("addressText")),
        latitude:body.has("latitude")?Number(Number(body.get("latitude")).toFixed(6)):stored?.latitude,
        longitude:body.has("longitude")?Number(Number(body.get("longitude")).toFixed(6)):stored?.longitude}
      return Response.json(stored,{status:init.method==="POST"?201:200})
    }
    if(url.endsWith("/properties/31"))return Response.json(stored)
    return Response.json({title:"Unexpected request"},{status:404})
  }))
  return {writes,searches,failNextWrite:()=>{rejectNextWrite=true}}
}
it("sends the final dragged pin, reopens the saved marker, and preserves it on a name-only edit",async()=>{
  const {writes}=fixture()
  const submit=vi.fn(async(request:PropertyWriteRequest)=>{await createProperty("test",request);return true})
  const view=render(<PropertyFormModal mode="create" accessToken="test" saving={false} onClose={vi.fn()} onSubmit={submit}/>)
  await choose("Tỉnh/Thành phố","Thành phố Hà Nội");await choose("Phường/Xã","Phường Ba Đình")
  fireEvent.change(screen.getByLabelText("Tên khu trọ"),{target:{value:"Trọ Việt Ba Đình"}})
  fireEvent.change(screen.getByLabelText("Số nhà, tên đường"),{target:{value:"12 Lê Lợi"}})
  fireEvent.click(await screen.findByRole("button",{name:"Bấm đặt ghim"}));fireEvent.click(screen.getByRole("button",{name:"Kéo ghim"}))
  expect((screen.getByLabelText("Số nhà, tên đường") as HTMLInputElement).value).toBe("12 Lê Lợi")
  fireEvent.click(screen.getByRole("button",{name:"Tạo khu trọ"}))
  await waitFor(()=>expect(writes).toHaveLength(1))
  expect(writes[0].get("latitude")).toBe("21.0334567");expect(writes[0].get("longitude")).toBe("105.8156789")
  view.unmount()
  const saved=await getProperty("test",31)
  const update=vi.fn(async(request:PropertyWriteRequest)=>{await updateProperty("test",31,request);return true})
  const edit=render(<PropertyFormModal mode="edit" accessToken="test" initialProperty={saved} saving={false} onClose={vi.fn()} onSubmit={update}/>)
  await waitFor(()=>expect(screen.getByLabelText("Ghim đang hiển thị").textContent).toBe("21.033457,105.815679"))
  await screen.findByText(/Vị trí đã lưu/)
  fireEvent.change(screen.getByLabelText("Tên khu trọ"),{target:{value:"Trọ Việt mới"}})
  fireEvent.click(screen.getByRole("button",{name:"Lưu khu trọ"}))
  await waitFor(()=>expect(writes).toHaveLength(2))
  expect(writes[1].has("latitude")).toBe(false);expect(writes[1].has("longitude")).toBe(false)
  fireEvent.click(screen.getByRole("button",{name:"Bấm đặt ghim"}));fireEvent.click(screen.getByRole("button",{name:"Lưu khu trọ"}))
  await waitFor(()=>expect(writes).toHaveLength(3));expect(writes[2].get("latitude")).toBe("21.032")
  edit.unmount()
  const moved=await getProperty("test",31)
  render(<PropertyFormModal mode="edit" accessToken="test" initialProperty={moved} saving={false} onClose={vi.fn()} onSubmit={update}/>)
  await waitFor(()=>expect(screen.getByLabelText("Ghim đang hiển thị").textContent).toBe("21.032,105.814"))
})
it("does not resurrect a selected pin when changing address A to B and back to A",async()=>{
  fixture();const submit=vi.fn(async()=>true)
  render(<PropertyFormModal mode="create" accessToken="test" saving={false} onClose={vi.fn()} onSubmit={submit}/>)
  await choose("Tỉnh/Thành phố","Thành phố Hà Nội");await choose("Phường/Xã","Phường Ba Đình")
  fireEvent.change(screen.getByLabelText("Tên khu trọ"),{target:{value:"Trọ Việt"}})
  const address=screen.getByLabelText("Số nhà, tên đường")
  fireEvent.change(address,{target:{value:"12 Lê Lợi"}});fireEvent.click(await screen.findByRole("button",{name:"Bấm đặt ghim"}))
  fireEvent.change(address,{target:{value:"14 Lê Lợi"}});fireEvent.change(address,{target:{value:"12 Lê Lợi"}})
  expect(screen.getByLabelText("Ghim đang hiển thị").textContent).toBe("Chưa có ghim")
  fireEvent.click(screen.getByRole("button",{name:"Tạo khu trọ"}))
  expect(submit).not.toHaveBeenCalled();expect(screen.getByText(/Hãy chọn ghim khu trọ/)).toBeTruthy()
})


it("searches directly, requires selecting the suggestion, saves the dragged pin and reopens it",async()=>{
  const {writes,searches}=fixture()
  const submit=vi.fn(async(request:PropertyWriteRequest)=>{await createProperty("admin-token",request);return true})
  const view=render(<PropertyFormModal mode="create" accessToken="admin-token" saving={false} onClose={vi.fn()} onSubmit={submit}/>)
  await choose("Tỉnh/Thành phố","Thành phố Hà Nội");await choose("Phường/Xã","Phường Ba Đình")
  fireEvent.change(screen.getByLabelText("Tên khu trọ"),{target:{value:"Trọ Việt A6"}})
  fireEvent.change(screen.getByLabelText("Số nhà, tên đường"),{target:{value:"18, đường Lê Lợi"}})
  expect(searches).toHaveLength(0)
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  const suggestion=await screen.findByRole("button",{name:/18 Lê Lợi, Hà Nội/})
  expect(searches).toHaveLength(1)
  expect(searches[0].init?.credentials).toBe("omit")
  expect(new Headers(searches[0].init?.headers).has("Authorization")).toBe(false)
  expect(new URL(searches[0].url).searchParams.get("q")).toContain("Phường Ba Đình")
  expect(screen.getByLabelText("Ghim đang hiển thị").textContent).toBe("Chưa có ghim")
  fireEvent.click(screen.getByRole("button",{name:"Tạo khu trọ"}))
  expect(writes).toHaveLength(0)
  fireEvent.click(suggestion)
  expect(screen.getByLabelText("Ghim đang hiển thị").textContent).toBe("21.033,105.815")
  fireEvent.click(screen.getByRole("button",{name:"Kéo ghim"}))
  fireEvent.click(screen.getByRole("button",{name:"Tạo khu trọ"}))
  await waitFor(()=>expect(writes).toHaveLength(1))
  expect(writes[0].get("addressText")).toBe("18, đường Lê Lợi")
  expect(writes[0].get("latitude")).toBe("21.0334567")
  expect(writes[0].has("confirmationToken")).toBe(false)
  view.unmount()
  const saved=await getProperty("admin-token",31)
  render(<PropertyFormModal mode="edit" accessToken="admin-token" initialProperty={saved} saving={false} onClose={vi.fn()} onSubmit={submit}/>)
  await waitFor(()=>expect(screen.getByLabelText("Ghim đang hiển thị").textContent).toBe("21.033457,105.815679"))
})
it("allows manual save after provider throttling and retains the pin after a failed save",async()=>{
  const {writes,searches,failNextWrite}=fixture(429)
  const submit=vi.fn(async(request:PropertyWriteRequest)=>{await createProperty("test",request);return true})
  render(<PropertyFormModal mode="create" accessToken="test" saving={false} onClose={vi.fn()} onSubmit={submit}/>)
  await choose("Tỉnh/Thành phố","Thành phố Hà Nội");await choose("Phường/Xã","Phường Ba Đình")
  fireEvent.change(screen.getByLabelText("Tên khu trọ"),{target:{value:"Trọ Việt A6 offline"}})
  fireEvent.change(screen.getByLabelText("Số nhà, tên đường"),{target:{value:"20 Lê Lợi"}})
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  await screen.findByText(/Dịch vụ tìm vị trí đang không khả dụng/)
  expect(searches).toHaveLength(1)
  fireEvent.click(screen.getByRole("button",{name:"Kéo ghim"}))
  failNextWrite()
  fireEvent.click(screen.getByRole("button",{name:"Tạo khu trọ"}))
  await screen.findByText("Không thể lưu khu trọ lúc này.")
  expect(screen.getByLabelText("Ghim đang hiển thị").textContent).toBe("21.0334567,105.8156789")
  fireEvent.click(screen.getByRole("button",{name:"Tạo khu trọ"}))
  await waitFor(()=>expect(writes).toHaveLength(2))
  expect(writes[1].get("latitude")).toBe(writes[0].get("latitude"))
})
