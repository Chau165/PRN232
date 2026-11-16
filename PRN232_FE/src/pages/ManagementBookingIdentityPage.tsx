import { useEffect,useRef,useState } from "react"
import { useAuth } from "@/auth/AuthContext"
import ManagementPage from "@/components/management/ManagementPage"
import { ApiError } from "@/api/apiClient"
import { listBookingApplicationReviews,getBookingApplicationReview,reviewBookingPerson,reviewBookingRoster,readReviewIdentity,type BookingApplication } from "@/services/bookingApplicationService"
const statusLabel:Record<string,string>={Incomplete:"Thiếu hồ sơ",PendingReview:"Chờ duyệt",Approved:"Đã duyệt",Rejected:"Bị từ chối"}
export default function ManagementBookingIdentityPage(){const {accessToken,user}=useAuth();return accessToken&&user?<ReviewSession key={`${user.id}:${accessToken}`} token={accessToken} role={user.role}/>:null}
function ReviewSession({token,role}:{token:string;role:string}){
 const [rows,setRows]=useState<BookingApplication[]>([]),[selected,setSelected]=useState<BookingApplication|null>(null)
 const [page,setPage]=useState(1),[reload,setReload]=useState(0),[loading,setLoading]=useState(true),[busy,setBusy]=useState(false),[error,setError]=useState("")
 const [notes,setNotes]=useState<Record<number,string>>({}),[rosterNote,setRosterNote]=useState(""),[preview,setPreview]=useState<string|null>(null)
 const lock=useRef(false),active=useRef(true),selectedId=useRef<number|null>(null),imageSequence=useRef(0)
 selectedId.current=selected?.bookingApplicationId??null
 useEffect(()=>{active.current=true;const controller=new AbortController();setLoading(true);setError("");setRows([]);setSelected(null);setPreview(null)
  listBookingApplicationReviews(token,page,controller.signal).then(value=>{if(!controller.signal.aborted)setRows(value)})
   .catch(()=>{if(!controller.signal.aborted)setError("Không tải được danh sách hồ sơ.")}).finally(()=>{if(!controller.signal.aborted)setLoading(false)})
  return()=>{active.current=false;imageSequence.current++;controller.abort()}
 },[token,page,reload])
 useEffect(()=>{if(!preview)return;const timer=setTimeout(()=>setPreview(null),120000);return()=>clearTimeout(timer)},[preview])
 async function decision(kind:"Identity"|"Roster",decision:string,personId?:number){
  if(!selected||lock.current)return
  const note=kind==="Roster"?rosterNote:notes[personId!]??""
  if(note.trim().length>500 || decision==="Rejected"&&!note.trim()){setError("Từ chối cần lý do, tối đa 500 ký tự.");return}
  lock.current=true;imageSequence.current++;setBusy(true);setError("");setPreview(null)
  try{
   const value=kind==="Roster"?await reviewBookingRoster(token,selected,decision,note):await reviewBookingPerson(token,selected,personId!,decision,note)
   if(active.current){setSelected(value);setRows(old=>old.map(a=>a.bookingApplicationId===value.bookingApplicationId?value:a));setNotes({});setRosterNote("")}
  }catch(cause){
   if(active.current){
    setError(cause instanceof Error?cause.message:"Không xét duyệt được hồ sơ.")
    if(cause instanceof ApiError&&[404,409].includes(cause.status??0)){
     try{const value=await getBookingApplicationReview(token,selected.bookingApplicationId);if(active.current){setSelected(value);setRows(old=>old.map(a=>a.bookingApplicationId===value.bookingApplicationId?value:a));setNotes({});setRosterNote("")}}
     catch{if(active.current){setSelected(null);setRows([])}}
    }
   }
  }finally{lock.current=false;if(active.current)setBusy(false)}
 }
 async function viewImage(personId:number,side:"Front"|"Back"){
  if(!selected)return;const id=selected.bookingApplicationId;const sequence=++imageSequence.current;setPreview(null);setError("")
  try{const value=await readReviewIdentity(token,selected,personId,side);if(!active.current||selectedId.current!==id||imageSequence.current!==sequence)return
   const url=new URL(value.url);if(url.protocol!=="https:"||url.username||url.password)throw Error("URL ảnh không hợp lệ.");setPreview(url.href)
  }catch{if(active.current&&selectedId.current===id&&imageSequence.current===sequence)setError("Không xem được ảnh hoặc quyền quản lý đã thay đổi.")}
 }
 return <ManagementPage title="Hồ sơ CCCD đặt cọc" description="Xét duyệt hồ sơ khách đã cọc; xác nhận đề xuất người ở trước khi lập hợp đồng.">
  <div className="space-y-4">
   <button type="button" disabled={busy||loading} onClick={()=>setReload(v=>v+1)} className="rounded border px-3 py-2">Tải lại hồ sơ</button>
   {loading&&<p role="status">Đang tải hồ sơ…</p>}{error&&<p role="alert" className="text-rose-700">{error}</p>}
   {!loading&&rows.length===0&&!error&&<p>Không có hồ sơ đã cọc trong phạm vi quản lý.</p>}
   <ul className="space-y-2">{rows.map(a=><li key={a.bookingApplicationId} className="rounded-xl bg-white p-4">
    <strong>{a.propertyName} · {a.roomCode} · Booking #{a.bookingId}</strong><p>{a.people[0]?.fullName} · {a.people.filter(p=>p.status==="PendingReview").length} người chờ duyệt</p>
    <button type="button" disabled={busy} className="text-teal-700 underline" onClick={()=>{setSelected(a);setNotes({});setRosterNote("");setPreview(null);setError("")}}>Xem hồ sơ #{a.bookingApplicationId}</button>
   </li>)}</ul>
   <nav aria-label="Phân trang hồ sơ CCCD" className="flex gap-4"><button disabled={page===1||busy||loading} onClick={()=>setPage(p=>p-1)}>Trang trước</button><span>Trang {page}</span><button disabled={rows.length<50||busy||loading} onClick={()=>setPage(p=>p+1)}>Trang sau</button></nav>
   {selected&&<section aria-label={`Chi tiết hồ sơ ${selected.bookingApplicationId}`} className="space-y-4 rounded-xl border bg-white p-5">
    <h2 className="font-bold">Chi tiết hồ sơ #{selected.bookingApplicationId}</h2>
    {!selected.canEdit&&<p className="text-amber-800">Hồ sơ đã khóa do booking thay đổi hoặc đã có hợp đồng.</p>}
    <p>Số người đã chốt: {selected.confirmedOccupantCount??selected.occupantCount} · Số người đề xuất: {selected.occupantCount}</p>
    {selected.people.map(p=><article key={p.personId} className="space-y-2 rounded-lg border p-4" aria-label={`Hồ sơ người ${p.personId}`}>
     <h3 className="font-bold">{p.position===0?"Người đứng tên":`Người ở cùng ${p.position}`} · {p.fullName}</h3>
     <p>CCCD: {p.maskedIdentityNumber??"Chưa có"} · {statusLabel[p.status]??p.status} · Phiên bản {p.revision??1}</p>
     {p.reviewNote&&<p className="text-rose-700">Lý do/ghi chú: {p.reviewNote}</p>}
     <div className="flex gap-4">{(["Front","Back"] as const).map(side=><button key={side} type="button" disabled={busy||!(side==="Front"?p.hasFront:p.hasBack)} className="text-teal-700 underline" onClick={()=>void viewImage(p.personId,side)}>Xem {side==="Front"?"mặt trước":"mặt sau"} · {p.fullName}</button>)}</div>
     {p.status==="PendingReview"&&selected.canEdit&&<>
      <label className="block">Lý do/ghi chú cho {p.fullName}<textarea className="block w-full rounded border p-2" aria-label={`Lý do cho ${p.personId}`} maxLength={500} disabled={busy} value={notes[p.personId]??""} onChange={e=>setNotes(old=>({...old,[p.personId]:e.target.value}))}/></label>
      <button type="button" disabled={busy} onClick={()=>void decision("Identity","Approved",p.personId)} className="mr-4 rounded bg-teal-700 px-3 py-2 text-white">Duyệt {p.fullName}</button>
      <button type="button" disabled={busy} onClick={()=>void decision("Identity","Rejected",p.personId)} className="text-rose-700 underline">Từ chối {p.fullName}</button>
     </>}
    </article>)}
    {selected.rosterChangeStatus==="Pending"&&<div className="space-y-3 rounded bg-amber-50 p-4">
     <h3 className="font-bold">Đề xuất thay danh sách/số người chờ Manager xác nhận</h3>
     {role==="MANAGER"&&selected.canEdit?<>
      <label>Lý do/ghi chú danh sách<textarea aria-label="Lý do danh sách" maxLength={500} disabled={busy} className="block w-full rounded border p-2" value={rosterNote} onChange={e=>setRosterNote(e.target.value)}/></label>
      <p>Duyệt đầy đủ hồ sơ từng người trước khi xác nhận danh sách.</p>
      <button disabled={busy||selected.people.some(p=>p.status!=="Approved")} onClick={()=>void decision("Roster","Approved")} className="mr-4 rounded bg-teal-700 px-3 py-2 text-white">Xác nhận danh sách mới</button>
      <button disabled={busy} onClick={()=>void decision("Roster","Rejected")} className="text-rose-700 underline">Từ chối danh sách mới</button>
     </>:<p>Manager đang phụ trách khu trọ sẽ xác nhận thay đổi này.</p>}
    </div>}
    {selected.rosterChangeNote&&<p>Ghi chú danh sách: {selected.rosterChangeNote}</p>}
   </section>}
   {preview&&<div className="rounded-xl border bg-white p-4"><img src={preview} referrerPolicy="no-referrer" alt="Ảnh CCCD đang xét duyệt" className="max-h-96"/><button onClick={()=>setPreview(null)}>Đóng ảnh CCCD</button></div>}
  </div>
 </ManagementPage>
}
