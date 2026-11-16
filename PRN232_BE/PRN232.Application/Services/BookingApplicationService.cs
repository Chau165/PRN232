using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
using PRN232.Application.Settings;
using PRN232.Application.States;
using PRN232.Application.Validation;
using PRN232.Domain.Entities;
using Microsoft.Extensions.Logging;

namespace PRN232.Application.Services;
public sealed class BookingApplicationService(IBookingApplicationRepository repository, IBookingRepository bookings,
    IBookingService bookingService, IBookingIdentityImageStorage storage, ICccdHasher hasher, IIdentityDocumentRepository identities,
    TimeProvider clock, BookingDepositSettings depositSettings, IUserContactLookup contacts,
    ILogger<BookingApplicationService> logger, INotificationRealtimePublisher realtime)
{
    private BookingApplicationDto Dto(BookingApplication a) => new(a.BookingApplicationId,a.RoomId,a.BookingId,a.Status,
        a.MoveInDate,a.OccupantCount,a.Version,a.People.OrderBy(p=>p.Position).Select(p=>new BookingApplicationPersonDto(
            p.BookingApplicationPersonId,p.Position,p.FullName,string.IsNullOrEmpty(p.IdentityNumber)?null:CccdMasking.MaskNumber(p.IdentityNumber),
            p.ConsentRecordedAtUtc.HasValue,p.Status,
            p.Images.Any(i=>i.Side=="Front" && i.IsCurrent && i.PersonRevision==p.Revision),
            p.Images.Any(i=>i.Side=="Back" && i.IsCurrent && i.PersonRevision==p.Revision))).ToArray(),depositSettings.AppliesToFirstMonth);
    private async Task<BookingApplication> Owned(int id,int customerId,CancellationToken token) =>
        await repository.FindAsync(id,customerId,token) ?? throw new BookingApplicationNotFoundException();
    private static void Editable(BookingApplication a,Guid version)
    {
        if(a.Status!="Draft" || a.BookingId.HasValue) throw new BookingApplicationConflictException("Hồ sơ đã gửi. Không thể sửa ở bước chuẩn bị.");
        if(version==Guid.Empty || a.Version!=version) throw new BookingApplicationConflictException("Hồ sơ đã thay đổi. Hãy tải lại.");
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
        if(existing is not null) return Dto(existing);
        var name=await repository.CustomerNameAsync(customerId,token) ?? throw new BookingApplicationNotFoundException();
        var a=new BookingApplication {CustomerId=customerId,RoomId=request.RoomId,MoveInDate=request.MoveInDate,OccupantCount=request.OccupantCount,
            CreatedAtUtc=clock.GetUtcNow().UtcDateTime,UpdatedAtUtc=clock.GetUtcNow().UtcDateTime};
        for(var index=0;index<a.OccupantCount;index++) a.People.Add(new(){Position=index,FullName=index==0?name:""});
        a=await repository.AddAsync(a,token);return Dto(a);
    }
    public async Task<BookingApplicationDto> GetAsync(int id,int customerId,CancellationToken token) => Dto(await Owned(id,customerId,token));
    public async Task<BookingApplicationDto> UpdateAsync(int id,UpdateBookingApplicationDto request,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);Editable(a,request.Version);await Validate(a.RoomId,request.MoveInDate,request.OccupantCount);
        var removed=a.People.Where(p=>p.Position>=request.OccupantCount).ToArray();
        if(removed.Any(p=>p.Images.Count>0 || !string.IsNullOrWhiteSpace(p.FullName) || !string.IsNullOrEmpty(p.IdentityNumber)) && !request.ConfirmRemovePeople)
            throw new BookingApplicationConflictException("Xác nhận xóa thông tin người ở cùng khi giảm số người.");
        var paths=removed.SelectMany(p=>p.Images).Select(i=>i.ObjectPath).ToArray();
        foreach(var p in removed) {a.People.Remove(p);repository.RemovePerson(p);}
        for(var index=a.People.Count;index<request.OccupantCount;index++) a.People.Add(new(){Position=index});
        a.MoveInDate=request.MoveInDate;a.OccupantCount=request.OccupantCount;Touch(a);await repository.SaveAsync(token);
        foreach(var path in paths) await Cleanup(path);
        return Dto(a);
    }
    public async Task<BookingApplicationDto> UpdatePersonAsync(int id,int personId,UpdateBookingPersonDto request,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);Editable(a,request.Version);
        var p=a.People.SingleOrDefault(p=>p.BookingApplicationPersonId==personId) ?? throw new BookingApplicationNotFoundException();
        var name=request.FullName?.Trim() ?? "";
        if(name.Length>100 || (p.Position==0 && name.Length==0)) throw new BookingApplicationValidationException("Họ tên phải có từ 1 đến 100 ký tự.");
        var number=request.IdentityNumber?.Trim();
        if(number is not null && (number.Length!=12 || !number.All(c=>c is >= '0' and <= '9'))) throw new BookingApplicationValidationException("Số CCCD phải gồm đúng 12 chữ số.");
        var hash=number is null?p.IdentityNumberHash:hasher.Hash(number);
        if(hash is not null && await identities.CccdNumberUsedByOtherAsync(hash,customerId)) throw new BookingApplicationValidationException("CCCD đã được đăng ký bởi tài khoản khác.");
        if(hash is not null && a.People.Any(other=>other!=p && other.IdentityNumberHash==hash)) throw new BookingApplicationValidationException("Không được đăng ký trùng CCCD trong hồ sơ.");
        var changed=name!=p.FullName || (number is not null && number!=p.IdentityNumber) || request.ConsentConfirmed!=p.ConsentRecordedAtUtc.HasValue;
        if(changed) {
            BookingApplicationPolicy.InvalidateReview(p);
            foreach(var image in p.Images.Where(i=>i.IsCurrent)) image.PersonRevision=p.Revision;
            p.FullName=name;if(number is not null) {p.IdentityNumber=number;p.IdentityNumberHash=hash;}
            p.ConsentRecordedAtUtc=request.ConsentConfirmed?clock.GetUtcNow().UtcDateTime:null;
        }
        Touch(a);await repository.SaveAsync(token);return Dto(a);
    }
    public async Task<BookingApplicationDto> UploadAsync(int id,int personId,string side,Guid version,BookingIdentityUpload image,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);Editable(a,version);
        var p=a.People.SingleOrDefault(p=>p.BookingApplicationPersonId==personId) ?? throw new BookingApplicationNotFoundException();
        if(side is not ("Front" or "Back")) throw new BookingApplicationValidationException("Mặt ảnh phải là Front hoặc Back.");
        if(!p.ConsentRecordedAtUtc.HasValue) throw new BookingApplicationValidationException("Lưu xác nhận đồng ý cung cấp CCCD trước khi tải ảnh.");
        StoredBookingIdentityImage uploaded;
        try {uploaded=await storage.UploadAsync(id,personId,side,image,token);}
        catch(ArgumentException) {throw new BookingApplicationValidationException("Ảnh phải là JPEG, PNG hoặc WebP hợp lệ, tối đa 5 MiB.");}
        catch(Exception ex) when(ex is HttpRequestException or InvalidOperationException || ex is TaskCanceledException && !token.IsCancellationRequested) {throw new BookingApplicationStorageException();}
        try {
            BookingApplicationPolicy.InvalidateReview(p);
            foreach(var old in p.Images.Where(i=>i.IsCurrent)) {if(old.Side==side) old.IsCurrent=false;else old.PersonRevision=p.Revision;}
            p.Images.Add(new(){Side=side,ObjectPath=uploaded.ObjectPath,ContentType=uploaded.ContentType,SizeBytes=uploaded.SizeBytes,
                PersonRevision=p.Revision,CreatedAtUtc=clock.GetUtcNow().UtcDateTime});
            Touch(a);await repository.SaveAsync(token);
        } catch {await Cleanup(uploaded.ObjectPath);throw;}
        return Dto(a);
    }
    public async Task<BookingApplicationDto> DeleteImageAsync(int id,int personId,string side,Guid version,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);Editable(a,version);
        var p=a.People.SingleOrDefault(p=>p.BookingApplicationPersonId==personId) ?? throw new BookingApplicationNotFoundException();
        if(side is not ("Front" or "Back")) throw new BookingApplicationValidationException("Mặt ảnh không hợp lệ.");
        BookingApplicationPolicy.InvalidateReview(p);
        foreach(var image in p.Images.Where(i=>i.IsCurrent)) {if(image.Side==side)image.IsCurrent=false;else image.PersonRevision=p.Revision;}
        Touch(a);await repository.SaveAsync(token);return Dto(a);
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
        Editable(a,request.Version);await Validate(a.RoomId,a.MoveInDate,a.OccupantCount);
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
        try {using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));await storage.DeleteAsync(path,timeout.Token);}
        catch {logger.LogWarning("Booking identity object cleanup needs retry; no identity data logged.");}
    }
}
public sealed class BookingApplicationNotFoundException : Exception;
public sealed class BookingApplicationValidationException(string message) : Exception(message);
public sealed class BookingApplicationConflictException(string message) : Exception(message);
public sealed class BookingApplicationStorageException : Exception;
