import {fireEvent,render,screen,waitFor,within} from "@testing-library/react"
import {beforeEach,expect,it,vi} from "vitest"
import {MemoryRouter} from "react-router"
import ManagementBookingIdentityPage from "@/pages/ManagementBookingIdentityPage"
import BookingProfilePage from "@/pages/BookingProfilePage"
import {ApiError} from "@/api/apiClient"
import type {BookingApplication} from "@/services/bookingApplicationService"
const mocks=vi.hoisted(()=>({role:"MANAGER",token:"token",list:vi.fn(),detail:vi.fn(),review:vi.fn(),roster:vi.fn(),read:vi.fn(),own:vi.fn(),save:vi.fn(),update:vi.fn(),upload:vi.fn(),remove:vi.fn(),resubmit:vi.fn()}))
vi.mock("@/auth/AuthContext",()=>({useAuth:()=>({accessToken:mocks.token,user:{id:"7",role:mocks.role}})}))
vi.mock("@/components/management/ManagementPage",()=>({default:({children}:{children:React.ReactNode})=><main>{children}</main>}))
vi.mock("@/components/customer/CustomerPageShell",()=>({default:({children}:{children:React.ReactNode})=><main>{children}</main>}))
vi.mock("@/services/bookingApplicationService",()=>({listBookingApplicationReviews:mocks.list,getBookingApplicationReview:mocks.detail,reviewBookingPerson:mocks.review,reviewBookingRoster:mocks.roster,readReviewIdentity:mocks.read,
 loadBookingApplicationByBooking:mocks.own,saveBookingPerson:mocks.save,updateBookingApplication:mocks.update,uploadBookingIdentity:mocks.upload,deleteBookingIdentity:mocks.remove,readBookingIdentity:mocks.read,resubmitBookingRoster:mocks.resubmit}))
const app=(status="PendingReview"):BookingApplication=>({bookingApplicationId:1,roomId:5,bookingId:7,status:"Submitted",moveInDate:"2026-11-16",occupantCount:1,version:"v1",depositAppliesToFirstMonth:true,
 canEdit:true,confirmedOccupantCount:1,bookingStatus:"Deposited",propertyName:"Trọ A",roomCode:"A101",rosterChangeStatus:"None",
 people:[{personId:2,position:0,fullName:"Customer A",maskedIdentityNumber:"********8901",consentConfirmed:true,status,hasFront:true,hasBack:true,revision:2}]})
beforeEach(()=>{Object.values(mocks).forEach(v=>{if(typeof v==="function")v.mockReset()});mocks.read.mockImplementation(async(_t,_a,_p,side)=>({url:`https://storage.test/${side}?token=test`}));mocks.role="MANAGER";mocks.token="token";mocks.list.mockResolvedValue([app()]);mocks.own.mockResolvedValue(app());mocks.detail.mockResolvedValue(app("Approved"))})
it("requires a rejection reason and shows the recorded result",async()=>{
 mocks.review.mockResolvedValue({...app("Rejected"),version:"v2",people:[{...app().people[0],status:"Rejected",reviewNote:"Ảnh bị mờ"}]})
 render(<ManagementBookingIdentityPage/>);fireEvent.click(await screen.findByRole("button",{name:"Xem hồ sơ #1"}))
 fireEvent.click(screen.getByRole("button",{name:"Từ chối Customer A"}));expect(mocks.review).not.toHaveBeenCalled();await screen.findByRole("alert")
 fireEvent.change(screen.getByLabelText("Lý do cho 2"),{target:{value:"Ảnh bị mờ"}});fireEvent.click(screen.getByRole("button",{name:"Từ chối Customer A"}))
 await screen.findByText("Lý do/ghi chú: Ảnh bị mờ");expect(mocks.review).toHaveBeenCalledTimes(1)
 expect(screen.queryByRole("button",{name:"Duyệt Customer A"})).toBeNull()
})
it("reloads a conflicting decision without overwriting another approval",async()=>{
 mocks.review.mockRejectedValue(new ApiError("Hồ sơ đã thay đổi. Hãy tải lại.",{status:409}))
 render(<ManagementBookingIdentityPage/>);fireEvent.click(await screen.findByRole("button",{name:"Xem hồ sơ #1"}));fireEvent.click(screen.getByRole("button",{name:"Duyệt Customer A"}))
 await waitFor(()=>expect(mocks.detail).toHaveBeenCalledWith("token",1))
 await screen.findByText(/Đã duyệt/);expect(screen.queryByRole("button",{name:"Duyệt Customer A"})).toBeNull();expect(mocks.review).toHaveBeenCalledTimes(1)
})
it("only managers can confirm a pending roster after person approvals",async()=>{
 const a={...app("Approved"),occupantCount:2,rosterChangeStatus:"Pending",people:[app("Approved").people[0],{...app("Approved").people[0],personId:3,position:1,fullName:"Roommate"}]};mocks.list.mockResolvedValue([a]);mocks.roster.mockResolvedValue({...a,confirmedOccupantCount:2,rosterChangeStatus:"Approved"})
 const view=render(<ManagementBookingIdentityPage/>);fireEvent.click(await screen.findByRole("button",{name:"Xem hồ sơ #1"}));fireEvent.click(screen.getByRole("button",{name:"Xác nhận danh sách mới"}));await waitFor(()=>expect(mocks.roster).toHaveBeenCalledTimes(1))
 await waitFor(()=>expect(screen.queryByRole("button",{name:"Xác nhận danh sách mới"})).toBeNull())
 view.unmount();mocks.role="ADMIN";render(<ManagementBookingIdentityPage/>);fireEvent.click(await screen.findByRole("button",{name:"Xem hồ sơ #1"}));expect(screen.queryByRole("button",{name:"Xác nhận danh sách mới"})).toBeNull()
})
it("customers see rejection reasons and can save corrections after deposit",async()=>{
 mocks.role="CUSTOMER";const a={...app("Rejected"),people:[{...app().people[0],status:"Rejected",reviewNote:"Kiểm tra lại họ tên"}]};mocks.own.mockResolvedValue(a);mocks.save.mockResolvedValue({...app(),version:"v2",people:[{...app().people[0],fullName:"Tên đã sửa"}]})
 render(<MemoryRouter initialEntries={["/booking-profile?bookingId=7"]}><BookingProfilePage/></MemoryRouter>)
 await screen.findByText("Lý do/ghi chú: Kiểm tra lại họ tên");fireEvent.change(screen.getByLabelText("Họ tên Bạn — Người đứng tên dự kiến"),{target:{value:"Tên đã sửa"}})
 fireEvent.click(screen.getByRole("button",{name:"Lưu thông tin của bạn"}));await waitFor(()=>expect(mocks.save).toHaveBeenCalledTimes(1));await screen.findByText("Trạng thái: Chờ xét duyệt")
 expect(screen.queryByText("Lý do/ghi chú: Kiểm tra lại họ tên")).toBeNull()
})
it("freezes edits after a contract exists",async()=>{
 mocks.role="CUSTOMER";mocks.own.mockResolvedValue({...app("Approved"),canEdit:false})
 render(<MemoryRouter initialEntries={["/booking-profile?bookingId=7"]}><BookingProfilePage/></MemoryRouter>);await screen.findByText(/Hồ sơ chỉ bổ sung khi đã cọc/)
 const fieldset=screen.getByText("Bạn — Người đứng tên dự kiến").closest("fieldset")!;expect(fieldset.disabled).toBe(true)
 expect(within(fieldset).getByRole("button",{name:"Lưu thông tin của bạn"})).toBeTruthy();expect(mocks.save).not.toHaveBeenCalled()
})

it("automatically displays both sides only when a profile is opened and hides internal metadata",async()=>{
 render(<ManagementBookingIdentityPage/>);const open=await screen.findByRole("button",{name:"Xem hồ sơ #1"});expect(mocks.read).not.toHaveBeenCalled();fireEvent.click(open)
 await screen.findByRole("img",{name:"CCCD mặt trước · Customer A"});await screen.findByRole("img",{name:"CCCD mặt sau · Customer A"})
 expect(mocks.read).toHaveBeenCalledTimes(2);expect(screen.queryByText(/Số người đã chốt/)).toBeNull();expect(screen.queryByText(/Phiên bản/)).toBeNull();expect(screen.queryByRole("button",{name:"Xem mặt trước · Customer A"})).toBeNull()
})
it.each([["Approved","bg-green-50"],["Rejected","bg-red-50"],["Incomplete","bg-orange-50"],["PendingReview","bg-white"]])("colors list, detail and person cards for %s",async(status,color)=>{
 mocks.list.mockResolvedValue([app(status)]);render(<ManagementBookingIdentityPage/>);const open=await screen.findByRole("button",{name:"Xem hồ sơ #1"});expect(open.closest("li")?.classList.contains(color)).toBe(true);fireEvent.click(open)
 expect(screen.getByRole("region",{name:"Chi tiết hồ sơ 1"}).classList.contains(color)).toBe(true)
 expect(screen.getByRole("article",{name:"Hồ sơ người 2"}).classList.contains(color)).toBe(true)
 await screen.findByRole("img",{name:"CCCD mặt trước · Customer A"})
})
it("shows a missing side without requesting it and retains the available side",async()=>{
 const a=app("Incomplete");a.people[0].hasBack=false;mocks.list.mockResolvedValue([a]);render(<ManagementBookingIdentityPage/>);fireEvent.click(await screen.findByRole("button",{name:"Xem hồ sơ #1"}))
 await screen.findByRole("img",{name:"CCCD mặt trước · Customer A"});expect(screen.getByText("Chưa có ảnh mặt sau.")).toBeTruthy();expect(mocks.read).toHaveBeenCalledTimes(1)
})
it("shows and retries a failed image without hiding the other side",async()=>{
 mocks.read.mockImplementation(async(_t,_a,_p,side)=>{if(side==="Front")throw new ApiError("Storage unavailable",{status:502});return {url:"https://storage.test/back"}})
 render(<ManagementBookingIdentityPage/>);fireEvent.click(await screen.findByRole("button",{name:"Xem hồ sơ #1"}));await screen.findByRole("img",{name:"CCCD mặt sau · Customer A"});await screen.findByText(/Không tải được ảnh mặt trước/)
 mocks.read.mockResolvedValue({url:"https://storage.test/retry"});fireEvent.click(screen.getByRole("button",{name:"Tải lại ảnh mặt trước"}));await screen.findByRole("img",{name:"CCCD mặt trước · Customer A"})
})
it("discards late signed URLs after switching profiles",async()=>{
 const second={...app(),bookingApplicationId:3,people:[{...app().people[0],personId:4,fullName:"Another customer"}]};mocks.list.mockResolvedValue([app(),second]);let resolve!:(value:{url:string})=>void
 mocks.read.mockImplementation((_t,a)=>a.bookingApplicationId===1?new Promise(r=>{resolve=r}):Promise.resolve({url:"https://storage.test/current"}))
 render(<ManagementBookingIdentityPage/>);fireEvent.click(await screen.findByRole("button",{name:"Xem hồ sơ #1"}));fireEvent.click(screen.getByRole("button",{name:"Xem hồ sơ #3"}));await screen.findByRole("img",{name:"CCCD mặt trước · Another customer"});resolve({url:"https://storage.test/old"});await waitFor(()=>expect(screen.queryByRole("img",{name:"CCCD mặt trước · Customer A"})).toBeNull())
})
