using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.Settings;
using PRN232.Application.States;
using PRN232.Application.Validation;
using PRN232.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace PRN232.Application.Services;
public sealed partial class BookingApplicationService(IBookingApplicationRepository repository, IBookingRepository bookings,
    IBookingService bookingService, IBookingIdentityImageStorage storage, ICccdHasher hasher, IIdentityDocumentRepository identities,
    TimeProvider clock, BookingDepositSettings depositSettings, IUserContactLookup contacts,
    ILogger<BookingApplicationService> logger, INotificationRealtimePublisher realtime, IBookingIdentityCleanupQueue? cleanupQueue = null)
{
    private async Task<BookingApplicationDto> Dto(BookingApplication a,CancellationToken token)
    {
        var context=await repository.ContextAsync(a,token);
        var canEdit=a.Status=="Draft" && !a.BookingId.HasValue || a.Status=="Submitted" && context.BookingStatus=="Deposited" && !context.HasContract;
        return new(a.BookingApplicationId,a.RoomId,a.BookingId,a.Status,a.MoveInDate,a.OccupantCount,a.Version,
            a.People.OrderBy(p=>p.Position).Select(p=>new BookingApplicationPersonDto(p.BookingApplicationPersonId,p.Position,p.FullName,
                string.IsNullOrEmpty(p.IdentityNumber)?null:CccdMasking.MaskNumber(p.IdentityNumber),p.ConsentRecordedAtUtc.HasValue,p.Status,
                p.Images.Any(i=>i.Side=="Front" && i.IsCurrent && i.PersonRevision==p.Revision),p.Images.Any(i=>i.Side=="Back" && i.IsCurrent && i.PersonRevision==p.Revision),p.ReviewNote,p.Revision)).ToArray(),
            depositSettings.AppliesToFirstMonth,a.RosterChangeStatus,a.RosterChangeNote,canEdit,context.ConfirmedOccupantCount,context.BookingStatus,context.PropertyName,context.RoomCode,canEdit && BookingApplicationPolicy.ReadyForContract(a) && context.ConfirmedOccupantCount==a.OccupantCount);
    }
    private async Task<BookingApplication> Owned(int id,int customerId,CancellationToken token) =>
        await repository.FindAsync(id,customerId,token) ?? throw new BookingApplicationNotFoundException();
    private async Task Editable(BookingApplication a,Guid version,CancellationToken token)
    {
        if(version==Guid.Empty || a.Version!=version)throw new BookingApplicationConflictException("Hồ sơ đã thay đổi. Hãy tải lại.");
        if(a.Status=="Draft" && !a.BookingId.HasValue)return;
        var context=await repository.ContextAsync(a,token);
        if(a.Status!="Submitted" || context.BookingStatus!="Deposited" || context.HasContract)
            throw new BookingApplicationConflictException("Chỉ bổ sung hồ sơ sau khi cọc và trước khi lập hợp đồng.");
    }
    private Task Persist(BookingApplication a,int customerId,CancellationToken token)=>a.Status=="Submitted"
        ? repository.SaveSubmittedAsync(a,customerId,RoleNames.Customer,null,token):repository.SaveAsync(token);
    private void MarkRosterPending(BookingApplication a)
    {
        a.RosterChangeStatus="Pending";a.RosterChangeNote=null;a.RosterReviewedById=null;a.RosterReviewedAtUtc=null;
        a.RosterRequestedAtUtc=clock.GetUtcNow().UtcDateTime;
    }
    private static void RefreshReviewStatus(BookingApplication a,BookingApplicationPerson p)
    {
        if(a.Status=="Submitted" && p.Status!="Approved")p.Status=BookingApplicationPolicy.HasCompleteIdentity(p)?"PendingReview":"Incomplete";
    }
    private async Task Validate(int roomId,DateOnly date,int count)
    {
        var room=await bookings.GetRoomAsync(roomId) ?? throw new BookingApplicationNotFoundException();
        if(count<1 || count>room.MaxOccupants) throw new BookingApplicationValidationException("Số người không phù hợp sức chứa phòng.");
        var today=BookingRules.Today(clock);
        if(date<today || date>today.AddDays(BookingRules.MoveInWindowDays)) throw new BookingApplicationValidationException("Ngày dọn vào phải trong 14 ngày kể từ hôm nay.");
    }
    private void Touch(BookingApplication a) {a.Version=Guid.NewGuid();a.UpdatedAtUtc=clock.GetUtcNow().UtcDateTime;}
    public async Task<BookingApplicationDto> CreateAsync(CreateBookingApplicationDto request,int customerId,CancellationToken token)
    {
        await Validate(request.RoomId,request.MoveInDate,request.OccupantCount);
        var existing=await repository.FindDraftAsync(request.RoomId,customerId,token);
        if(existing is not null) return await Dto(existing,token);
        var name=await repository.CustomerNameAsync(customerId,token) ?? throw new BookingApplicationNotFoundException();
        var a=new BookingApplication {CustomerId=customerId,RoomId=request.RoomId,MoveInDate=request.MoveInDate,OccupantCount=request.OccupantCount,
            CreatedAtUtc=clock.GetUtcNow().UtcDateTime,UpdatedAtUtc=clock.GetUtcNow().UtcDateTime};
        for(var index=0;index<a.OccupantCount;index++) a.People.Add(new(){Position=index,FullName=index==0?name:""});
        a=await repository.AddAsync(a,token);return await Dto(a,token);
    }
    public async Task<BookingApplicationDto> GetAsync(int id,int customerId,CancellationToken token) => await Dto(await Owned(id,customerId,token),token);
    public async Task<BookingApplicationDto> UpdateAsync(int id,UpdateBookingApplicationDto request,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);await Editable(a,request.Version,token);
        if(a.Status=="Draft")await Validate(a.RoomId,request.MoveInDate,request.OccupantCount);
        else {
            var context=await repository.ContextAsync(a,token);
            if(request.MoveInDate!=a.MoveInDate || request.OccupantCount<1 || request.OccupantCount>context.Capacity)
                throw new BookingApplicationValidationException("Sau cọc chỉ đề xuất thay số người; ngày dọn vào giữ nguyên.");
            if(request.OccupantCount!=a.OccupantCount)MarkRosterPending(a);
        }
        var removed=a.People.Where(p=>p.Position>=request.OccupantCount).ToArray();
        if(removed.Any(p=>p.Images.Count>0 || !string.IsNullOrWhiteSpace(p.FullName) || !string.IsNullOrEmpty(p.IdentityNumber)) && !request.ConfirmRemovePeople)
            throw new BookingApplicationConflictException("Xác nhận xóa thông tin người ở cùng khi giảm số người.");
        var paths=removed.SelectMany(p=>p.Images).Select(i=>i.ObjectPath).ToArray();
        foreach(var p in removed) {a.People.Remove(p);repository.RemovePerson(p);}
        for(var index=a.People.Count;index<request.OccupantCount;index++) a.People.Add(new(){Position=index});
        a.MoveInDate=request.MoveInDate;a.OccupantCount=request.OccupantCount;Touch(a);await Persist(a,customerId,token);
        foreach(var path in paths) await Cleanup(path);
        return await Dto(a,token);
    }
    public async Task<BookingApplicationDto> UpdatePersonAsync(int id,int personId,UpdateBookingPersonDto request,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);await Editable(a,request.Version,token);
        var p=a.People.SingleOrDefault(p=>p.BookingApplicationPersonId==personId) ?? throw new BookingApplicationNotFoundException();
        var name=request.FullName?.Trim() ?? "";
        if(name.Length>100 || (p.Position==0 && name.Length==0)) throw new BookingApplicationValidationException("Họ tên phải có từ 1 đến 100 ký tự.");
        var number=request.IdentityNumber?.Trim();
        if(number is not null && (number.Length!=12 || !number.All(c=>c is >= '0' and <= '9'))) throw new BookingApplicationValidationException("Số CCCD phải gồm đúng 12 chữ số.");
        var hash=number is null?p.IdentityNumberHash:hasher.Hash(number);
        if(hash is not null && await identities.CccdNumberUsedByOtherAsync(hash,customerId)) throw new BookingApplicationValidationException("CCCD đã được đăng ký bởi tài khoản khác.");
        if(hash is not null && a.People.Any(other=>other!=p && other.IdentityNumberHash==hash)) throw new BookingApplicationValidationException("Không được đăng ký trùng CCCD trong hồ sơ.");
        var changed=name!=p.FullName || (number is not null && number!=p.IdentityNumber) || request.ConsentConfirmed!=p.ConsentRecordedAtUtc.HasValue;
        if(a.Status=="Submitted" && p.Position>0 && (!string.IsNullOrWhiteSpace(p.FullName) || !string.IsNullOrEmpty(p.IdentityNumber)) &&
            (name!=p.FullName || number is not null && number!=p.IdentityNumber))MarkRosterPending(a);
        if(changed || p.Status=="Rejected") {
            BookingApplicationPolicy.InvalidateReview(p);
            foreach(var image in p.Images.Where(i=>i.IsCurrent)) {
                // A different CCCD must not inherit the previous person's verified photos.
                if(number is not null && number!=p.IdentityNumber && !string.IsNullOrEmpty(p.IdentityNumber))image.IsCurrent=false;
                else image.PersonRevision=p.Revision;
            }
            p.FullName=name;if(number is not null) {p.IdentityNumber=number;p.IdentityNumberHash=hash;}
            p.ConsentRecordedAtUtc=request.ConsentConfirmed?clock.GetUtcNow().UtcDateTime:null;
        }
        RefreshReviewStatus(a,p);
        Touch(a);await Persist(a,customerId,token);return await Dto(a,token);
    }
    public async Task<BookingApplicationDto> UploadAsync(int id,int personId,string side,Guid version,BookingIdentityUpload image,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);await Editable(a,version,token);
        var p=a.People.SingleOrDefault(p=>p.BookingApplicationPersonId==personId) ?? throw new BookingApplicationNotFoundException();
        if(side is not ("Front" or "Back")) throw new BookingApplicationValidationException("Mặt ảnh phải là Front hoặc Back.");
        if(!p.ConsentRecordedAtUtc.HasValue) throw new BookingApplicationValidationException("Lưu xác nhận đồng ý cung cấp CCCD trước khi tải ảnh.");
        StoredBookingIdentityImage uploaded;
        try {uploaded=await storage.UploadAsync(id,personId,side,image,token);}
        catch(BookingIdentityUploadUncertainException ex) {await Cleanup(ex.ObjectPath);throw new BookingApplicationStorageException();}
        catch(ArgumentException) {throw new BookingApplicationValidationException("Ảnh phải là JPEG, PNG hoặc WebP hợp lệ, tối đa 5 MiB.");}
        catch(Exception ex) when(ex is HttpRequestException or InvalidOperationException || ex is TaskCanceledException && !token.IsCancellationRequested) {throw new BookingApplicationStorageException();}
        try {
            BookingApplicationPolicy.InvalidateReview(p);
            foreach(var old in p.Images.Where(i=>i.IsCurrent)) {if(old.Side==side) old.IsCurrent=false;else old.PersonRevision=p.Revision;}
            p.Images.Add(new(){Side=side,ObjectPath=uploaded.ObjectPath,ContentType=uploaded.ContentType,SizeBytes=uploaded.SizeBytes,
                PersonRevision=p.Revision,CreatedAtUtc=clock.GetUtcNow().UtcDateTime});
            RefreshReviewStatus(a,p);
            Touch(a);await Persist(a,customerId,token);
        } catch {await Cleanup(uploaded.ObjectPath);throw;}
        return await Dto(a,token);
    }
    public async Task<BookingApplicationDto> DeleteImageAsync(int id,int personId,string side,Guid version,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);await Editable(a,version,token);
        var p=a.People.SingleOrDefault(p=>p.BookingApplicationPersonId==personId) ?? throw new BookingApplicationNotFoundException();
        if(side is not ("Front" or "Back")) throw new BookingApplicationValidationException("Mặt ảnh không hợp lệ.");
        BookingApplicationPolicy.InvalidateReview(p);
        foreach(var image in p.Images.Where(i=>i.IsCurrent)) {if(image.Side==side)image.IsCurrent=false;else image.PersonRevision=p.Revision;}
        RefreshReviewStatus(a,p);
        Touch(a);await Persist(a,customerId,token);return await Dto(a,token);
    }
    public async Task<string> ReadImageAsync(int id,int personId,string side,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);
        var image=a.People.Where(p=>p.BookingApplicationPersonId==personId).SelectMany(p=>p.Images).SingleOrDefault(i=>i.IsCurrent && i.Side==side)
            ?? throw new BookingApplicationNotFoundException();
        try{return await storage.GetReadUrlAsync(image.ObjectPath,token);}
        catch(Exception ex) when(ex is HttpRequestException or InvalidOperationException) {throw new BookingApplicationStorageException();}
    }
    public async Task<BookingDto> SubmitAsync(int id,SubmitBookingApplicationDto request,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);
        if(a.BookingId.HasValue) return await bookingService.GetByIdAsync(a.BookingId.Value,customerId,RoleNames.Customer) ?? throw new BookingApplicationNotFoundException();
        await Editable(a,request.Version,token);await Validate(a.RoomId,a.MoveInDate,a.OccupantCount);
        var room=await bookings.GetRoomAsync(a.RoomId) ?? throw new BookingApplicationNotFoundException();
        if(!BookingApplicationPolicy.CanSubmit(a,room.MaxOccupants)) throw new BookingApplicationValidationException("Bạn cần hoàn tất họ tên, CCCD, hai mặt ảnh và xác nhận đồng ý trước khi giữ phòng.");
        await PhoneNumberRequiredException.EnsureAsync(contacts,customerId,PhoneNumberRequiredException.SelfMessage);
        var now=clock.GetUtcNow();
        var booking=new Bookings {RoomId=a.RoomId,CustomerId=customerId,ViewingId=request.ViewingId,MoveInDate=a.MoveInDate,OccupantCount=a.OccupantCount,
            DepositAmount=room.MonthlyRent,AgreedMonthlyRent=room.MonthlyRent,DepositAppliesToFirstMonth=depositSettings.AppliesToFirstMonth,
            Status=BookingStatuses.Pending,ExpiryDate=now.Add(BookingService.PaymentWindow).UtcDateTime};
        Notifications? pendingNotification=null;
        var bookingId=await repository.SubmitAsync(a,booking,bookingId=>pendingNotification=new(){UserId=room.OwnerId,Title=$"Booking #{bookingId} - Phòng {room.RoomCode} đang chờ thanh toán cọc",Type=NotificationTypes.Booking,IsRead=false},now,a.People.Where(BookingApplicationPolicy.HasCompleteIdentity).Select(p=>p.BookingApplicationPersonId).ToArray(),token);
        if(pendingNotification is not null) {
            try {await realtime.PublishCreatedAsync([pendingNotification]);}
            catch {logger.LogWarning("Booking committed; realtime notification delivery needs refresh.");}
        }
        return await bookingService.GetByIdAsync(bookingId,customerId,RoleNames.Customer) ?? throw new BookingApplicationNotFoundException();
    }
    private async Task Cleanup(string path)
    {
        try {using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));if(cleanupQueue is not null) await cleanupQueue.EnqueueAsync(path,clock.GetUtcNow().UtcDateTime,timeout.Token);}
        catch {logger.LogWarning("Booking identity object cleanup needs retry; no identity data logged.");}
    }
}
public sealed class BookingApplicationNotFoundException : Exception;
public sealed class BookingApplicationValidationException(string message) : Exception(message);
public sealed class BookingApplicationConflictException(string message) : Exception(message);
public sealed class BookingApplicationStorageException : Exception;
