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
function fixture(){
  let stored:PropertyDto|null=null
  const writes:FormData[]=[]
  vi.stubGlobal("fetch",vi.fn(async(input,init?:RequestInit)=>{
    const url=String(input)
    if(url.endsWith("/locations/provinces"))return Response.json([{provinceCode:"01",provinceName:"Thành phố Hà Nội"}])
    if(url.includes("/locations/wards?"))return Response.json([{wardCode:"00004",wardName:"Phường Ba Đình"}])
    if(init?.method==="POST"||init?.method==="PUT"){
      const body=init.body as FormData;writes.push(body)
      stored={propertyId:31,ownerId:9,ownerName:"Admin",roomCount:0,ward:"Phường Ba Đình",city:"Thành phố Hà Nội",provinceCode:"01",wardCode:"00004",propertyName:String(body.get("propertyName")),addressText:String(body.get("addressText")),
        latitude:body.has("latitude")?Number(Number(body.get("latitude")).toFixed(6)):stored?.latitude,
        longitude:body.has("longitude")?Number(Number(body.get("longitude")).toFixed(6)):stored?.longitude}
      return Response.json(stored,{status:init.method==="POST"?201:200})
    }
    if(url.endsWith("/properties/31"))return Response.json(stored)
    return Response.json({title:"Unexpected request"},{status:404})
  }))
  return {writes}
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
