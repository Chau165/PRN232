import {fireEvent,render,screen,waitFor} from "@testing-library/react"
import {beforeEach,expect,it,vi} from "vitest"
import ContractIdentitySnapshots from "@/components/customer/ContractIdentitySnapshots"
const mocks=vi.hoisted(()=>({request:vi.fn(),token:"customer-token"}))
vi.mock("@/auth/AuthContext",()=>({useAuth:()=>({accessToken:mocks.token})}))
vi.mock("@/api/apiClient",()=>({apiRequest:mocks.request}))
beforeEach(()=>{mocks.token="customer-token";mocks.request.mockReset()})
it("loads masked snapshots on demand and signs an image only after a click",async()=>{
 mocks.request.mockResolvedValueOnce([{snapshotId:5,position:0,fullName:"Nguyễn An",maskedIdentityNumber:"********1234"}]).mockResolvedValueOnce({url:"https://example.test/private?token=short-lived"})
 render(<ContractIdentitySnapshots contractId={12}/>);expect(mocks.request).not.toHaveBeenCalled()
 fireEvent.click(screen.getByText("Xem hồ sơ CCCD đã chốt"));await screen.findByText(/Nguyễn An/)
 fireEvent.click(screen.getByText("Mặt trước"));await screen.findByAltText("Ảnh CCCD trong hồ sơ hợp đồng")
 expect(mocks.request).toHaveBeenLastCalledWith("/contracts/12/identity-snapshots/5/images/Front/read-url",expect.objectContaining({method:"POST",accessToken:"customer-token"}))
 fireEvent.click(screen.getByText("Đóng ảnh CCCD"));expect(screen.queryByRole("img")).toBeNull()
})
it("discards an image response after the signed-in user changes",async()=>{
 let resolve!:(value:unknown)=>void
 mocks.request.mockResolvedValueOnce([{snapshotId:5,position:0,fullName:"Nguyễn An",maskedIdentityNumber:"********1234"}]).mockReturnValueOnce(new Promise(r=>{resolve=r}))
 const view=render(<ContractIdentitySnapshots contractId={12}/>);fireEvent.click(screen.getByText("Xem hồ sơ CCCD đã chốt"));await screen.findByText(/Nguyễn An/);fireEvent.click(screen.getByText("Mặt trước"))
 mocks.token="other-user";view.rerender(<ContractIdentitySnapshots contractId={12}/>);resolve({url:"https://example.test/private"})
 await waitFor(()=>expect(screen.queryByRole("img")).toBeNull());expect(screen.queryByText(/Nguyễn An/)).toBeNull()
})
