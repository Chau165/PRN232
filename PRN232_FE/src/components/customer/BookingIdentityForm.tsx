import { useEffect, useRef, useState } from "react"
import { Link } from "react-router"
import { ApiError } from "@/api/apiClient"
import { routes } from "@/constants/routes"
import { pendingBookingIdFromError } from "@/utils/bookingDisplay"
import { prepareBookingApplication,loadBookingApplication,updateBookingApplication,saveBookingPerson,uploadBookingIdentity,
 deleteBookingIdentity,readBookingIdentity,submitBookingApplication,type BookingApplication,type ApplicationPerson } from "@/services/bookingApplicationService"

type Props={token:string;userId:string;roomId:number;moveInDate:string;occupantCount:number;viewingId?:number|null;validate:()=>boolean;onSubmitted:(id:number)=>void;onBusyChange:(busy:boolean)=>void}
const inputClass="block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm"
export default function BookingIdentityForm(props:Props){
 return <BookingIdentityFormSession key={`${props.userId}:${props.roomId}:${props.token}`} {...props}/>
}
function BookingIdentityFormSession(props:Props){
 const {token,userId,roomId,moveInDate,occupantCount}=props
 const key=`booking-application:${userId}:${roomId}`
 const [app,setApp]=useState<BookingApplication|null>(null)
 const [busy,setBusy]=useState(false),[error,setError]=useState(""),[pendingId,setPendingId]=useState<number|null>(null)
 const [dirty,setDirty]=useState<Record<number,boolean>>({})
 const [reloadGeneration,setReloadGeneration]=useState(0)
 const locked=useRef(false),mounted=useRef(true)
 const mismatch=app!==null && (app.moveInDate!==moveInDate || app.occupantCount!==occupantCount)
 const remember=(value:BookingApplication)=>{setApp(value);setDirty(old=>Object.fromEntries(value.people.map(p=>[p.personId,old[p.personId]??false])));try{sessionStorage.setItem(key,String(value.bookingApplicationId))}catch{/* Browser storage is optional. */}}
 useEffect(()=>{
  mounted.current=true
  const controller=new AbortController()
  let saved:number|null=null;try{saved=Number(sessionStorage.getItem(key))||null}catch{/* optional */}
  if(saved){setBusy(true);loadBookingApplication(token,saved,controller.signal).then(value=>{if(!controller.signal.aborted)remember(value)})
   .catch(cause=>{if(!controller.signal.aborted){if(cause instanceof ApiError && cause.status===404){try{sessionStorage.removeItem(key)}catch{}}else setError("Không tải được hồ sơ đã lưu. Bấm tải lại hồ sơ để thử lại.")}})
   .finally(()=>{if(!controller.signal.aborted)setBusy(false)})}
  return()=>{mounted.current=false;controller.abort()}
 },[key,token])
 useEffect(()=>{props.onBusyChange(busy)},[busy,props.onBusyChange])
 async function run(action:()=>Promise<void>){
  if(locked.current)return false;locked.current=true;setBusy(true);setError("");setPendingId(null)
  try{await action();return true}catch(cause){if(mounted.current){setError(cause instanceof Error?cause.message:"Không cập nhật được hồ sơ.");setPendingId(pendingBookingIdFromError(cause))}return false}
  finally{locked.current=false;if(mounted.current)setBusy(false)}
 }
 const mutate=(action:()=>Promise<BookingApplication>)=>run(async()=>{const value=await action();if(mounted.current)remember(value)})
 async function prepare(){
  if(!props.validate())return
  const needsConfirmation=app && occupantCount<app.occupantCount && app.people.some(p=>p.position>=occupantCount && (dirty[p.personId]||p.fullName||p.maskedIdentityNumber||p.hasFront||p.hasBack))
  if(needsConfirmation && !window.confirm("Giảm số người sẽ xóa hồ sơ và ảnh của người ở cùng bị bỏ. Cậu có muốn tiếp tục?"))return
  await mutate(()=>app?updateBookingApplication(token,app,moveInDate,occupantCount,Boolean(needsConfirmation)):prepareBookingApplication(token,roomId,moveInDate,occupantCount))
 }
 const holder=app?.people.find(p=>p.position===0)
 const ready=holder && holder.fullName && holder.maskedIdentityNumber && holder.consentConfirmed && holder.hasFront && holder.hasBack
 return <section aria-label="Hồ sơ người thuê" className="space-y-4">
  <h2 className="font-bold text-slate-800">Hồ sơ người thuê</h2>
  <p className="text-sm text-slate-600">Bạn cần cung cấp đủ CCCD hai mặt trước khi giữ phòng. Người ở cùng có thể bổ sung sau khi cọc, trước khi lập hợp đồng. Chuẩn bị hồ sơ chưa giữ phòng và chưa bắt đầu 15 phút thanh toán.</p>
  {!app || mismatch ? <button type="button" disabled={busy} className="rounded-lg border border-teal-700 px-4 py-2" onClick={()=>void prepare()}>{app?"Cập nhật ngày và số người trong hồ sơ":"Chuẩn bị hồ sơ CCCD"}</button>:null}
  <button type="button" disabled={busy} className="ml-3 text-sm text-teal-700 underline" onClick={()=>void run(async()=>{
   if(Object.values(dirty).some(Boolean) && !window.confirm("Tải lại sẽ bỏ thay đổi chưa lưu. Tiếp tục?"))return
   let id=app?.bookingApplicationId;try{id??=Number(sessionStorage.getItem(key))||undefined}catch{}
   if(id){remember(await loadBookingApplication(token,id));setDirty({});setReloadGeneration(value=>value+1)}else throw new Error("Bấm Chuẩn bị hồ sơ CCCD để bắt đầu.")
  })}>Tải lại hồ sơ</button>
  {app?.status==="Draft" && app.people.map(person=><PersonEditor key={`${person.personId}:${reloadGeneration}`} person={person} disabled={busy||mismatch}
   onDirty={value=>setDirty(old=>({...old,[person.personId]:value}))}
   onSave={(name,number,consent)=>mutate(()=>saveBookingPerson(token,app,person.personId,name,number,consent))}
   onUpload={(side,file,details)=>mutate(async()=>{
    let current=app
    if(details){current=await saveBookingPerson(token,current,person.personId,details.fullName,details.identityNumber,details.consentConfirmed);if(!mounted.current)throw new DOMException("Session ended","AbortError");remember(current)}
    return uploadBookingIdentity(token,current,person.personId,side,file)
   })}
   onDelete={side=>mutate(()=>deleteBookingIdentity(token,app,person.personId,side))}
   onRead={side=>readBookingIdentity(token,app,person.personId,side)}/>)}
  {app && <p className="rounded-lg bg-teal-50 p-3 text-sm">{app.depositAppliesToFirstMonth?"Cọc được khấu trừ vào tiền phòng theo điều khoản booking; phần tiền phòng còn lại và dịch vụ được tính trong hóa đơn.":"Cọc là khoản bảo đảm, không tự khấu trừ tiền phòng tháng đầu; việc hoàn cọc theo điều khoản booking."}</p>}
  {app?.status==="Draft" && <p className="text-xs text-slate-500">Ảnh được lưu riêng tư. Hồ sơ của Bạn phải đầy đủ; tải ảnh lên không có nghĩa hồ sơ đã được duyệt.</p>}
  {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
  {pendingId && <Link className="text-teal-700 underline" to={`${routes.deposit}?bookingId=${pendingId}`}>Xem đặt phòng đang chờ thanh toán</Link>}
  <button type="button" className="w-full rounded-lg bg-teal-700 py-3 font-bold text-white disabled:opacity-50"
   disabled={busy || !app || mismatch || (!app.bookingId && (!ready || Object.values(dirty).some(Boolean)))}
   onClick={()=>void run(async()=>{if(!app||!props.validate())return;const booking=await submitBookingApplication(token,app,props.viewingId);if(mounted.current)props.onSubmitted(booking.bookingId)})}>
   {busy?"Đang xử lý hồ sơ…":app?.bookingId?"Tiếp tục thanh toán booking đã tạo":"Giữ phòng và thanh toán cọc"}
  </button>
 </section>
}
export type IdentityEdits={fullName:string;identityNumber:string|null;consentConfirmed:boolean}
export function PersonEditor({person,disabled,onDirty,onSave,onUpload,onDelete,onRead}:{person:ApplicationPerson;disabled:boolean;onDirty:(dirty:boolean)=>void;
 onSave:(name:string,number:string|null,consent:boolean)=>Promise<boolean>;onUpload:(side:"Front"|"Back",file:File,details?:IdentityEdits)=>Promise<boolean>;onDelete:(side:"Front"|"Back")=>Promise<boolean>;onRead:(side:"Front"|"Back")=>Promise<{url:string}>}){
 const [name,setName]=useState(person.fullName),[number,setNumber]=useState(""),[consent,setConsent]=useState(person.consentConfirmed)
 const [dirty,setDirty]=useState(false),[error,setError]=useState("")
 const [preview,setPreview]=useState<string|null>(null)
 const previewTimer=useRef<ReturnType<typeof setTimeout>|undefined>(undefined)
 const active=useRef(true)
 useEffect(()=>{active.current=true;return()=>{active.current=false;clearTimeout(previewTimer.current)}},[])
 const changed=()=>{setDirty(true);onDirty(true)}
 const label=person.position===0?"Bạn — Người đứng tên dự kiến":`Người ở cùng ${person.position}`
 return <fieldset disabled={disabled} className="space-y-3 rounded-xl border border-slate-200 p-4">
  <legend className="font-bold">{label}</legend>
  <p className="text-sm">Trạng thái: {({Incomplete:"Thiếu thông tin",PendingReview:"Chờ xét duyệt",Approved:"Đã duyệt",Rejected:"Bị từ chối"} as Record<string,string>)[person.status]??person.status}</p>
  {person.reviewNote && <p className="text-sm text-rose-700">Lý do/ghi chú: {person.reviewNote}</p>}
  <label className="block text-sm">Họ tên {person.position===0?"của bạn":"người ở cùng"}<input aria-label={`Họ tên ${label}`} className={inputClass} value={name} maxLength={100} onChange={event=>{setName(event.target.value);changed()}}/></label>
  <label className="block text-sm">Số CCCD (12 chữ số)<input aria-label={`Số CCCD ${label}`} className={inputClass} value={number} placeholder={person.maskedIdentityNumber??"Nhập 12 chữ số"} inputMode="numeric" maxLength={12} autoComplete="off" onChange={event=>{setNumber(event.target.value.replace(/\D/g,""));changed()}}/></label>
  <label className="flex gap-2 text-sm"><input type="checkbox" checked={consent} onChange={event=>{setConsent(event.target.checked);changed()}}/>{person.position===0?"Tôi đồng ý cung cấp thông tin và ảnh CCCD để thực hiện thuê phòng.":"Tôi đã được người ở cùng đồng ý cung cấp thông tin và ảnh CCCD."}</label>
  <button type="button" className="rounded-lg border border-teal-700 px-3 py-2 text-sm" onClick={async()=>{
   if(number && number.length!==12){setError("Số CCCD phải đủ 12 chữ số.");return}
   const saved=await onSave(name,number||null,consent)
   if(saved){setDirty(false);onDirty(false);setNumber("");setError("");setPreview(null)}
  }}>Lưu thông tin {person.position===0?"của bạn":`người ở cùng ${person.position}`}</button>
  {dirty && <p className="text-xs text-amber-800">Thông tin chưa lưu. Khi chọn ảnh, hệ thống sẽ lưu thông tin trước rồi tải ảnh lên.</p>}
  <p className="text-sm text-slate-600">{consent?"Chọn ảnh mặt trước và mặt sau (JPEG, PNG hoặc WebP, tối đa 5 MiB mỗi ảnh). Nếu chưa lưu, thông tin của người này sẽ được lưu tự động trước khi tải ảnh.":"Để bật nút chọn ảnh, hãy tích ô đồng ý cung cấp thông tin và ảnh CCCD ở trên."}</p>
  {(["Front","Back"] as const).map(side=><div key={side} className="space-y-1 text-sm">
   <label className="block">CCCD {side==="Front"?"mặt trước":"mặt sau"} · {(side==="Front"?person.hasFront:person.hasBack)?"Đã tải":"Chưa tải"}
    <input aria-label={`${label} CCCD ${side==="Front"?"mặt trước":"mặt sau"}`} type="file" accept="image/jpeg,image/png,image/webp" disabled={disabled||!consent} onChange={async event=>{
     const file=event.target.files?.[0];event.target.value="";if(!file)return
     if(file.size<=0||file.size>5*1024*1024||!["image/jpeg","image/png","image/webp"].includes(file.type)){setError("Chọn ảnh JPEG, PNG hoặc WebP tối đa 5 MiB.");return}
     if(!name.trim() || (!number && !person.maskedIdentityNumber) || (number && !/^\d{12}$/.test(number))){setError("Nhập họ tên và CCCD đủ 12 chữ số trước khi tải ảnh.");return}
     if(!consent){setError("Vui lòng đồng ý cung cấp thông tin và ảnh CCCD.");return}
     setError("");setPreview(null)
     const details=dirty||!person.consentConfirmed?{fullName:name,identityNumber:number||null,consentConfirmed:consent}:undefined
     const uploaded=await onUpload(side,file,details)
     if(uploaded&&active.current){setDirty(false);onDirty(false);setNumber("")}
    }}/>
   </label>
   {(side==="Front"?person.hasFront:person.hasBack) && <div className="flex gap-3">
    <button type="button" disabled={disabled||dirty} className="text-teal-700 underline" onClick={async()=>{try{const value=await onRead(side);if(!active.current)return;const url=new URL(value.url);if(url.protocol!=="https:"||url.username||url.password)throw Error("Link xem ảnh không hợp lệ.");setPreview(url.href);clearTimeout(previewTimer.current);previewTimer.current=setTimeout(()=>setPreview(null),120000)}catch{setError("Không xem được ảnh. Hãy thử lại.")}}}>Xem {side==="Front"?"mặt trước":"mặt sau"}</button>
    <button type="button" disabled={disabled||dirty} className="text-rose-700 underline" onClick={async()=>{setPreview(null);await onDelete(side)}}>Xóa {side==="Front"?"mặt trước":"mặt sau"}</button>
   </div>}
  </div>)}
  {preview && <div><img src={preview} referrerPolicy="no-referrer" alt={`Ảnh CCCD ${label}`} className="max-h-64 rounded-lg"/><button type="button" onClick={()=>setPreview(null)}>Đóng ảnh</button></div>}
  {error && <p role="alert" className="text-sm text-rose-700">{error}</p>}
 </fieldset>
}
