import { lazy, Suspense, useEffect, useRef, useState } from "react"
import { previewPropertyLocation, type LocationAddress, type LocationPreview } from "@/services/propertyLocationService"
import type { LocationPoint } from "./PropertyLocationMap"
const LocationMap=lazy(()=>import("./PropertyLocationMap"))
export default function PropertyLocationPicker({address,accessToken,initialPoint,disabled,onChange}:{address:LocationAddress;accessToken:string;initialPoint?:LocationPoint;disabled:boolean;onChange:(point:LocationPoint|null)=>void}) {
  const key=JSON.stringify(address)
  const currentKey=useRef(key);currentKey.current=key
  const request=useRef<AbortController|null>(null)
  const [preview,setPreview]=useState<LocationPreview|null>(null)
  const [point,setPoint]=useState<LocationPoint|null>(initialPoint??null)
  const [busy,setBusy]=useState(false)
  const [error,setError]=useState("")
  useEffect(()=>()=>request.current?.abort(),[])
  useEffect(()=>{request.current?.abort();setBusy(false);setPreview(null);setPoint(initialPoint??null);setError("")},[key])
  const ready=Boolean(address.addressText.trim()&&address.provinceCode&&address.wardCode)
  function pick(p:LocationPoint){request.current?.abort();setBusy(false);setPoint(p);setError("");onChange(p)}
  async function search(){
    request.current?.abort();const controller=new AbortController();request.current=controller;const started=key
    setBusy(true);setError("");onChange(null);setPreview(null);setPoint(null)
    try{const result=await previewPropertyLocation(accessToken,address,controller.signal);if(!controller.signal.aborted&&currentKey.current===started)setPreview(result)}
    catch(e){if(!controller.signal.aborted&&currentKey.current===started)setError(e instanceof Error?e.message:"Không tìm được vị trí.")}
    finally{if(request.current===controller)setBusy(false)}
  }
  return <section className="space-y-3 sm:col-span-2" aria-label="Vị trí khu trọ">
    <button type="button" disabled={disabled||busy||!ready} onClick={search} className="rounded-lg bg-teal-700 px-4 py-2 text-sm font-bold text-white disabled:opacity-50">{busy?"Đang tìm vị trí…":"Tìm thử"}</button>
    <p className="text-xs text-slate-600">Chọn gợi ý hoặc bấm/kéo ghim đến đúng nhà trọ rồi lưu. Kéo ghim không đổi địa chỉ đã nhập. Hệ thống chưa tự xác minh ranh giới phường.</p>
    {preview?.candidates.length===0&&<p role="status" className="text-sm text-amber-800">{preview.status==="Unavailable"?"Dịch vụ tìm vị trí đang không khả dụng. Bạn vẫn có thể chọn ghim thủ công.":"Chưa có kết quả phù hợp. Hãy kiểm tra địa chỉ hoặc chọn ghim thủ công."}</p>}
    {preview&&preview.candidates.length>0&&<ul className="max-h-52 space-y-2 overflow-auto">{preview.candidates.map((candidate,i)=><li key={i}><button type="button" disabled={disabled||busy} onClick={()=>pick(candidate.location.coordinates)} className="w-full rounded-lg border border-slate-200 p-3 text-left text-sm hover:border-teal-700">
      <span className="block font-semibold">{candidate.location.displayAddress}</span><span className="text-xs text-slate-500">{candidate.location.precision==="street"?"Vị trí gần đúng cấp đường phố":"Vị trí gợi ý"}{candidate.reasons.includes("ward_unverified")?" · Chưa xác minh phường/xã":""}</span>
    </button></li>)}</ul>}
    <Suspense fallback={<p>Đang tải bản đồ…</p>}><LocationMap point={point} initialPoint={initialPoint} onPick={p=>{if(!disabled)pick(p)}} /></Suspense>
    {point&&<p className="text-xs text-slate-600">Tọa độ sẽ lưu: {point.latitude.toFixed(6)}, {point.longitude.toFixed(6)}</p>}
    {error&&<p role="alert" className="rounded-lg bg-rose-50 p-3 text-sm text-rose-700">{error}</p>}
  </section>
}
