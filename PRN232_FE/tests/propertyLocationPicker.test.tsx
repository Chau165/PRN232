import {afterEach,beforeEach,it,expect,vi} from "vitest"
import {cleanup,fireEvent,render,screen,waitFor} from "@testing-library/react"
import PropertyLocationPicker from "@/components/management/PropertyLocationPicker"
const mocks=vi.hoisted(()=>({preview:vi.fn(),locate:vi.fn()}))
vi.mock("@/services/propertyLocationService",()=>({previewPropertyLocation:mocks.preview,locatePropertyArea:mocks.locate}))
vi.mock("@/components/management/PropertyLocationMap",()=>({default:({onPick,focus}:{onPick:(p:{latitude:number;longitude:number})=>void;focus?:{latitude:number;longitude:number;zoom:number}|null})=><><button type="button" onClick={()=>onPick({latitude:10.86,longitude:106.78})}>Kéo ghim kiểm thử</button><output aria-label="Vùng bản đồ">{focus?`${focus.latitude},${focus.longitude},${focus.zoom}`:""}</output></>}))
const address={addressText:"121 Lê Văn Việt",provinceCode:"79",wardCode:"01",provinceName:"Thành phố Hồ Chí Minh",wardName:"Phường Thủ Đức"}
const candidate={location:{displayAddress:"Lê Văn Việt, Thủ Đức",precision:"street",provider:"Photon",coordinates:{latitude:10.85,longitude:106.77}},matchLevel:"Suggestion",reasons:["ward_unverified"]}
beforeEach(()=>{mocks.preview.mockReset();mocks.locate.mockReset();mocks.locate.mockResolvedValue(null)});afterEach(cleanup)
it("selects a suggestion and sends the final dragged pin without a confirmation endpoint",async()=>{
  mocks.preview.mockResolvedValue({status:"Suggestions",candidates:[candidate],providers:[]})
  const change=vi.fn();render(<PropertyLocationPicker address={address} disabled={false} onChange={change}/>)
  expect(mocks.preview).not.toHaveBeenCalled()
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  fireEvent.click(await screen.findByRole("button",{name:/Lê Văn Việt, Thủ Đức/}))
  expect(change).toHaveBeenLastCalledWith({latitude:10.85,longitude:106.77})
  fireEvent.click(await screen.findByRole("button",{name:"Kéo ghim kiểm thử"}))
  expect(change).toHaveBeenLastCalledWith({latitude:10.86,longitude:106.78})
  expect(screen.queryByRole("button",{name:"Xác nhận vị trí"})).toBeNull()
})
it("ignores late results after the address changed",async()=>{
  let finish:(v:unknown)=>void=()=>{throw Error("No request")};mocks.preview.mockImplementation(()=>new Promise(resolve=>{finish=resolve}))
  const change=vi.fn();const view=render(<PropertyLocationPicker address={address} disabled={false} onChange={change}/>)
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  view.rerender(<PropertyLocationPicker address={{...address,addressText:"122 Lê Văn Việt"}} disabled={false} onChange={change}/>)
  finish({status:"Suggestions",candidates:[candidate],providers:[]})
  await waitFor(()=>expect(screen.queryByRole("button",{name:/Lê Văn Việt, Thủ Đức/})).toBeNull())
})
it("allows manual selection when search is unavailable",async()=>{
  mocks.preview.mockResolvedValue({status:"Unavailable",candidates:[],providers:[]})
  const change=vi.fn();render(<PropertyLocationPicker address={address} disabled={false} onChange={change}/>)
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  await screen.findByText(/Dịch vụ tìm vị trí đang không khả dụng/)
  fireEvent.click(await screen.findByRole("button",{name:"Kéo ghim kiểm thử"}))
  expect(change).toHaveBeenLastCalledWith({latitude:10.86,longitude:106.78})
})

it("keeps the current pin when a same-address lookup fails",async()=>{
  mocks.preview.mockRejectedValue(new Error("Mạng tìm kiếm không khả dụng"))
  const change=vi.fn();render(<PropertyLocationPicker address={address} initialPoint={{latitude:10.85,longitude:106.77}} disabled={false} onChange={change}/>)
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  await screen.findByText("Mạng tìm kiếm không khả dụng")
  expect(screen.getByText(/Vị trí đã lưu: 10.850000, 106.770000/)).toBeTruthy()
  expect(change).not.toHaveBeenCalled()
})
it("does not accept map edits while saving",async()=>{
  const change=vi.fn();render(<PropertyLocationPicker address={address} disabled onChange={change}/>)
  fireEvent.click(await screen.findByRole("button",{name:"Kéo ghim kiểm thử"}))
  expect(change).not.toHaveBeenCalled()
})
it("flies the map to the selected ward and again when the ward changes",async()=>{
  mocks.locate.mockResolvedValueOnce({latitude:10.85,longitude:106.77,zoom:15}).mockResolvedValueOnce({latitude:10.88,longitude:106.83,zoom:15})
  const view=render(<PropertyLocationPicker address={address} disabled={false} onChange={vi.fn()}/>)
  await waitFor(()=>expect(screen.getByLabelText("Vùng bản đồ").textContent).toBe("10.85,106.77,15"))
  expect(mocks.locate).toHaveBeenLastCalledWith({wardName:"Phường Thủ Đức",provinceName:"Thành phố Hồ Chí Minh"},expect.any(AbortSignal))
  view.rerender(<PropertyLocationPicker address={{...address,wardCode:"02",wardName:"Phường Long Bình"}} disabled={false} onChange={vi.fn()}/>)
  await waitFor(()=>expect(screen.getByLabelText("Vùng bản đồ").textContent).toBe("10.88,106.83,15"))
  expect(mocks.locate).toHaveBeenLastCalledWith({wardName:"Phường Long Bình",provinceName:"Thành phố Hồ Chí Minh"},expect.any(AbortSignal))
})
it("keeps a stored location's view until another ward is chosen",async()=>{
  const view=render(<PropertyLocationPicker address={address} initialPoint={{latitude:10.8,longitude:106.7}} disabled={false} onChange={vi.fn()}/>)
  await Promise.resolve()
  expect(mocks.locate).not.toHaveBeenCalled()
  view.rerender(<PropertyLocationPicker address={{...address,wardCode:"02",wardName:"Phường Long Bình"}} initialPoint={{latitude:10.8,longitude:106.7}} disabled={false} onChange={vi.fn()}/>)
  await waitFor(()=>expect(mocks.locate).toHaveBeenCalledTimes(1))
})
