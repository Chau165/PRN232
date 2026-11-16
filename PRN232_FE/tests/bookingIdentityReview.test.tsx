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
beforeEach(()=>{Object.values(mocks).forEach(v=>{if(typeof v==="function")v.mockReset()});mocks.role="MANAGER";mocks.token="token";mocks.list.mockResolvedValue([app()]);mocks.own.mockResolvedValue(app());mocks.detail.mockResolvedValue(app("Approved"))})
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
 await screen.findByText("Số người đã chốt: 2 · Số người đề xuất: 2")
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
