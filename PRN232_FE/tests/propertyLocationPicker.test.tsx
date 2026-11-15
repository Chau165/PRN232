import {afterEach,beforeEach,it,expect,vi} from "vitest"
import {cleanup,fireEvent,render,screen,waitFor} from "@testing-library/react"
import PropertyLocationPicker from "@/components/management/PropertyLocationPicker"
const mocks=vi.hoisted(()=>({preview:vi.fn()}))
vi.mock("@/services/propertyLocationService",()=>({previewPropertyLocation:mocks.preview}))
vi.mock("@/components/management/PropertyLocationMap",()=>({default:({onPick}:{onPick:(p:{latitude:number;longitude:number})=>void})=><button type="button" onClick={()=>onPick({latitude:10.86,longitude:106.78})}>Kéo ghim kiểm thử</button>}))
const address={addressText:"121 Lê Văn Việt",provinceCode:"79",wardCode:"01"}
const candidate={location:{displayAddress:"Lê Văn Việt, Thủ Đức",precision:"street",provider:"Photon",coordinates:{latitude:10.85,longitude:106.77}},matchLevel:"Suggestion",reasons:["ward_unverified"]}
beforeEach(()=>{mocks.preview.mockReset()});afterEach(cleanup)
it("selects a suggestion and sends the final dragged pin without a confirmation endpoint",async()=>{
  mocks.preview.mockResolvedValue({status:"Suggestions",candidates:[candidate],providers:[]})
  const change=vi.fn();render(<PropertyLocationPicker address={address} accessToken="jwt" disabled={false} onChange={change}/>)
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  fireEvent.click(await screen.findByRole("button",{name:/Lê Văn Việt, Thủ Đức/}))
  expect(change).toHaveBeenLastCalledWith({latitude:10.85,longitude:106.77})
  fireEvent.click(await screen.findByRole("button",{name:"Kéo ghim kiểm thử"}))
  expect(change).toHaveBeenLastCalledWith({latitude:10.86,longitude:106.78})
  expect(screen.queryByRole("button",{name:"Xác nhận vị trí"})).toBeNull()
})
it("ignores late results after the address changed",async()=>{
  let finish:(v:unknown)=>void=()=>{throw Error("No request")};mocks.preview.mockImplementation(()=>new Promise(resolve=>{finish=resolve}))
  const change=vi.fn();const view=render(<PropertyLocationPicker address={address} accessToken="jwt" disabled={false} onChange={change}/>)
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  view.rerender(<PropertyLocationPicker address={{...address,addressText:"122 Lê Văn Việt"}} accessToken="jwt" disabled={false} onChange={change}/>)
  finish({status:"Suggestions",candidates:[candidate],providers:[]})
  await waitFor(()=>expect(screen.queryByRole("button",{name:/Lê Văn Việt, Thủ Đức/})).toBeNull())
})
it("allows manual selection when search is unavailable",async()=>{
  mocks.preview.mockResolvedValue({status:"Unavailable",candidates:[],providers:[]})
  const change=vi.fn();render(<PropertyLocationPicker address={address} accessToken="jwt" disabled={false} onChange={change}/>)
  fireEvent.click(screen.getByRole("button",{name:"Tìm thử"}))
  await screen.findByText(/Dịch vụ tìm vị trí đang không khả dụng/)
  fireEvent.click(await screen.findByRole("button",{name:"Kéo ghim kiểm thử"}))
  expect(change).toHaveBeenLastCalledWith({latitude:10.86,longitude:106.78})
})
