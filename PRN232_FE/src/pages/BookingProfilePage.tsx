import { useEffect,useRef,useState } from "react"
import { useSearchParams,Link } from "react-router"
import { useAuth } from "@/auth/AuthContext"
import CustomerPageShell from "@/components/customer/CustomerPageShell"
import { PersonEditor } from "@/components/customer/BookingIdentityForm"
import { ApiError } from "@/api/apiClient"
import { routes } from "@/constants/routes"
import { loadBookingApplicationByBooking,updateBookingApplication,saveBookingPerson,uploadBookingIdentity,deleteBookingIdentity,readBookingIdentity,resubmitBookingRoster,type BookingApplication } from "@/services/bookingApplicationService"
export default function BookingProfilePage(){
 const {accessToken,user}=useAuth();const [search]=useSearchParams();const raw=search.get("bookingId")??"";const id=/^[1-9]\d*$/.test(raw)?Number(raw):0
 return <CustomerPageShell title="Hồ sơ người thuê" description="Bổ sung hồ sơ sau cọc và theo dõi kết quả xét duyệt.">
  {accessToken&&user&&id>0?<Profile key={`${user.id}:${accessToken}:${id}`} token={accessToken} bookingId={id}/>:<p role="alert">Không xác định được booking.</p>}
 </CustomerPageShell>
}
function Profile({token,bookingId}:{token:string;bookingId:number}){
 const [app,setApp]=useState<BookingApplication|null>(null),[busy,setBusy]=useState(false),[error,setError]=useState("")
 const [count,setCount]=useState(""),[reload,setReload]=useState(0),[generation,setGeneration]=useState(0),[dirty,setDirty]=useState<Record<number,boolean>>({})
 const lock=useRef(false),active=useRef(true)
 useEffect(()=>{active.current=true;const controller=new AbortController();setBusy(true);setError("")
  loadBookingApplicationByBooking(token,bookingId,controller.signal).then(value=>{if(!controller.signal.aborted){setApp(value);setCount(String(value.occupantCount));setDirty({});setGeneration(g=>g+1)}})
   .catch(cause=>{if(!controller.signal.aborted)setError(cause instanceof ApiError&&cause.status===404?"Booking này chưa có hồ sơ CCCD mới; hãy liên hệ quản lý.":"Không tải được hồ sơ.")})
   .finally(()=>{if(!controller.signal.aborted)setBusy(false)})
  return()=>{active.current=false;controller.abort()}
 },[token,bookingId,reload])
 async function mutate(action:()=>Promise<BookingApplication>){if(lock.current)return false;lock.current=true;setBusy(true);setError("")
  try{const value=await action();if(active.current){setApp(value);setDirty(old=>Object.fromEntries(value.people.map(p=>[p.personId,old[p.personId]??false])))}return true}
  catch(cause){if(active.current)setError(cause instanceof Error?cause.message:"Không lưu được hồ sơ.");return false}
  finally{lock.current=false;if(active.current)setBusy(false)}
 }
 return <div className="space-y-4 rounded-xl bg-white p-5">
  <Link to={routes.myBookings} className="text-teal-700 underline">Về lịch xem & đặt cọc</Link>
  <button type="button" disabled={busy} className="ml-4 text-teal-700 underline" onClick={()=>{if(Object.values(dirty).some(Boolean)&&!window.confirm("Tải lại sẽ bỏ thông tin chưa lưu. Tiếp tục?"))return;setReload(r=>r+1)}}>Tải lại hồ sơ</button>
  {busy&&<p role="status">Đang xử lý hồ sơ…</p>}{error&&<p role="alert" className="text-rose-700">{error}</p>}
  {app&&<>
   <h2 className="font-bold">Booking #{bookingId} · {app.roomCode}</h2>
   <p>Số người đã chốt: {app.confirmedOccupantCount??app.occupantCount} · Đề xuất trong hồ sơ: {app.occupantCount}</p>
   <p>Đề xuất thay người/số người: {({None:"Chưa có thay đổi",Pending:"Chờ Manager xác nhận",Approved:"Đã xác nhận",Rejected:"Bị từ chối"} as Record<string,string>)[app.rosterChangeStatus??"None"]}</p>
   {app.rosterChangeNote&&<p className="text-rose-700">Lý do/ghi chú danh sách: {app.rosterChangeNote}</p>}
   {!app.canEdit&&<p className="text-amber-800">Hồ sơ chỉ bổ sung khi đã cọc và chưa lập hợp đồng. Booking chờ trả tiền, đã hủy hoặc đã có hợp đồng sẽ khóa sửa.</p>}
   <p className="text-sm text-slate-600">Sửa họ tên, CCCD hoặc ảnh đã duyệt sẽ phải xét duyệt lại. Thay người/số người sau cọc là đề xuất; booking chỉ cập nhật khi Manager xác nhận. Không đổi khoản tiền đã thu.</p>
   <label className="block">Số người đề xuất<input className="ml-3 rounded border px-3 py-2" type="number" min={1} value={count} disabled={busy||!app.canEdit} onChange={e=>setCount(e.target.value)}/></label>
   <button type="button" disabled={busy||!app.canEdit||Number(count)===app.occupantCount} className="rounded bg-teal-700 px-4 py-2 text-white disabled:opacity-50" onClick={()=>{
    const n=Number(count);if(!Number.isInteger(n)||n<1){setError("Số người phải là số nguyên dương.");return}
    if(Object.values(dirty).some(Boolean)){setError("Lưu thông tin từng người trước khi thay số người.");return}
    if(!window.confirm("Gửi đề xuất thay số người? Giảm số người sẽ bỏ hồ sơ của người ở cùng cuối danh sách; booking giữ số đã chốt đến khi Manager duyệt."))return
    void mutate(()=>updateBookingApplication(token,app,app.moveInDate,n,true))
   }}>Đề xuất thay số người</button>
   {app.canEdit&&app.rosterChangeStatus==="Rejected"&&<button type="button" disabled={busy||Object.values(dirty).some(Boolean)} onClick={()=>void mutate(()=>resubmitBookingRoster(token,app))}>Gửi lại đề xuất danh sách</button>}
   {app.people.map(person=><PersonEditor key={`${person.personId}:${generation}`} person={person} disabled={busy||!app.canEdit}
    onDirty={v=>setDirty(old=>({...old,[person.personId]:v}))}
    onSave={(name,number,consent)=>mutate(()=>saveBookingPerson(token,app,person.personId,name,number,consent))}
    onUpload={(side,file,details)=>mutate(async()=>{
     let current=app
     if(details){current=await saveBookingPerson(token,current,person.personId,details.fullName,details.identityNumber,details.consentConfirmed);if(!active.current)throw new DOMException("Session ended","AbortError");setApp(current)}
     return uploadBookingIdentity(token,current,person.personId,side,file)
    })}
    onDelete={side=>mutate(()=>deleteBookingIdentity(token,app,person.personId,side))}
    onRead={side=>readBookingIdentity(token,app,person.personId,side)}/>)}
  </>}
 </div>
}
