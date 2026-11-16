import { apiRequest } from "@/api/apiClient"
import type { BookingDto } from "@/types/booking"
export type ApplicationPerson = { personId:number;position:number;fullName:string;maskedIdentityNumber:string|null;consentConfirmed:boolean;status:string;hasFront:boolean;hasBack:boolean;reviewNote?:string|null;revision?:number }
export type BookingApplication = {bookingApplicationId:number;roomId:number;bookingId:number|null;status:string;moveInDate:string;occupantCount:number;version:string;people:ApplicationPerson[];depositAppliesToFirstMonth:boolean;rosterChangeStatus?:string;rosterChangeNote?:string|null;canEdit?:boolean;confirmedOccupantCount?:number|null;bookingStatus?:string|null;propertyName?:string|null;roomCode?:string|null;readyForContract?:boolean}
const root="/booking-applications"
export const loadBookingApplication=(token:string,id:number,signal?:AbortSignal)=>apiRequest<BookingApplication>(`${root}/${id}`,{accessToken:token,signal,cache:"no-store",requireJson:true})
export const prepareBookingApplication=(token:string,roomId:number,moveInDate:string,occupantCount:number)=>apiRequest<BookingApplication>(root,{accessToken:token,method:"POST",json:{roomId,moveInDate,occupantCount},requireJson:true})
export const updateBookingApplication=(token:string,app:BookingApplication,moveInDate:string,occupantCount:number,confirmRemovePeople:boolean)=>apiRequest<BookingApplication>(`${root}/${app.bookingApplicationId}`,{accessToken:token,method:"PATCH",json:{moveInDate,occupantCount,confirmRemovePeople,version:app.version},requireJson:true})
export const saveBookingPerson=(token:string,app:BookingApplication,personId:number,fullName:string,identityNumber:string|null,consentConfirmed:boolean)=>apiRequest<BookingApplication>(`${root}/${app.bookingApplicationId}/people/${personId}`,{accessToken:token,method:"PUT",json:{fullName,identityNumber,consentConfirmed,version:app.version},requireJson:true})
export const uploadBookingIdentity=(token:string,app:BookingApplication,personId:number,side:"Front"|"Back",file:File)=>{
 const body=new FormData();body.append("file",file)
 return apiRequest<BookingApplication>(`${root}/${app.bookingApplicationId}/people/${personId}/images/${side}?version=${encodeURIComponent(app.version)}`,{accessToken:token,method:"POST",body,requireJson:true})
}
export const deleteBookingIdentity=(token:string,app:BookingApplication,personId:number,side:"Front"|"Back")=>apiRequest<BookingApplication>(`${root}/${app.bookingApplicationId}/people/${personId}/images/${side}?version=${encodeURIComponent(app.version)}`,{accessToken:token,method:"DELETE",requireJson:true})
export const readBookingIdentity=(token:string,app:BookingApplication,personId:number,side:"Front"|"Back")=>apiRequest<{url:string}>(`${root}/${app.bookingApplicationId}/people/${personId}/images/${side}/read-url`,{accessToken:token,method:"POST",requireJson:true})
export const submitBookingApplication=(token:string,app:BookingApplication,viewingId?:number|null)=>apiRequest<BookingDto>(`${root}/${app.bookingApplicationId}/submit`,{accessToken:token,method:"POST",json:{version:app.version,viewingId:viewingId??null},requireJson:true})

export const loadBookingApplicationByBooking=(token:string,bookingId:number,signal?:AbortSignal)=>apiRequest<BookingApplication>(`${root}/by-booking/${bookingId}`,{accessToken:token,signal,cache:"no-store",requireJson:true})
export const resubmitBookingRoster=(token:string,app:BookingApplication)=>apiRequest<BookingApplication>(`${root}/${app.bookingApplicationId}/roster-resubmit`,{accessToken:token,method:"POST",json:{version:app.version},requireJson:true})
const reviews="/booking-application-reviews"
export const listBookingApplicationReviews=(token:string,page:number,signal?:AbortSignal)=>apiRequest<BookingApplication[]>(`${reviews}?page=${page}`,{accessToken:token,signal,cache:"no-store",requireJson:true})
export const getBookingApplicationReview=(token:string,id:number,signal?:AbortSignal)=>apiRequest<BookingApplication>(`${reviews}/${id}`,{accessToken:token,signal,cache:"no-store",requireJson:true})
export const reviewBookingPerson=(token:string,app:BookingApplication,personId:number,decision:string,note:string)=>apiRequest<BookingApplication>(`${reviews}/${app.bookingApplicationId}/people/${personId}/review`,{accessToken:token,method:"POST",json:{decision,note,version:app.version},requireJson:true})
export const reviewBookingRoster=(token:string,app:BookingApplication,decision:string,note:string)=>apiRequest<BookingApplication>(`${reviews}/${app.bookingApplicationId}/roster-review`,{accessToken:token,method:"POST",json:{decision,note,version:app.version},requireJson:true})
export const readReviewIdentity=(token:string,app:Pick<BookingApplication,"bookingApplicationId">,personId:number,side:"Front"|"Back",signal?:AbortSignal)=>apiRequest<{url:string}>(`${reviews}/${app.bookingApplicationId}/people/${personId}/images/${side}/read-url`,{accessToken:token,method:"POST",signal,requireJson:true})

export const getContractApplicationSource=(token:string,bookingId:number,signal?:AbortSignal)=>apiRequest<BookingApplication>(`${reviews}/by-booking/${bookingId}/contract-source`,{accessToken:token,signal,cache:"no-store",requireJson:true})
