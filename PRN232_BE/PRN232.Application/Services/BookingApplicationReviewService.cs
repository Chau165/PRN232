using PRN232.Application.DTOs;
using PRN232.Application.Policies;
using PRN232.Application.States;
using PRN232.Domain.Entities;
namespace PRN232.Application.Services;
public sealed partial class BookingApplicationService
{
    public async Task<BookingApplicationDto> ContractSourceAsync(int bookingId,int actorId,string role,CancellationToken token)=>
        await Dto(await repository.FindReviewByBookingAsync(bookingId,actorId,role,token) ?? throw new BookingApplicationNotFoundException(),token);
    private async Task<BookingApplication> ForReview(int id,int actorId,string role,CancellationToken token)=>
        await repository.FindForReviewAsync(id,actorId,role,token) ?? throw new BookingApplicationNotFoundException();
    public async Task<BookingApplicationDto> GetByBookingAsync(int bookingId,int customerId,CancellationToken token)=>
        await Dto(await repository.FindByBookingAsync(bookingId,customerId,token) ?? throw new BookingApplicationNotFoundException(),token);
    public async Task<List<BookingApplicationDto>> ReviewListAsync(int actorId,string role,int page,CancellationToken token)
    {
        if(role is not (RoleNames.Admin or RoleNames.Manager))throw new BookingApplicationNotFoundException();
        if(page<1 || page>10000)throw new BookingApplicationValidationException("Trang không hợp lệ.");
        var applications=await repository.ReviewListAsync(actorId,role,page,token);
        var result=new List<BookingApplicationDto>();foreach(var a in applications)result.Add(await Dto(a,token));return result;
    }
    public async Task<BookingApplicationDto> GetReviewAsync(int id,int actorId,string role,CancellationToken token)=>await Dto(await ForReview(id,actorId,role,token),token);
    private static string? ReviewNote(ReviewBookingApplicationDto dto)
    {
        if(dto.Decision is not ("Approved" or "Rejected"))throw new BookingApplicationValidationException("Chọn Approved hoặc Rejected.");
        var note=dto.Note?.Trim();
        if(note?.Length>500 || dto.Decision=="Rejected" && string.IsNullOrWhiteSpace(note))throw new BookingApplicationValidationException("Từ chối cần lý do, tối đa 500 ký tự.");
        return string.IsNullOrWhiteSpace(note)?null:note;
    }
    public async Task<BookingApplicationDto> ReviewPersonAsync(int id,int personId,ReviewBookingApplicationDto dto,int actorId,string role,CancellationToken token)
    {
        var a=await ForReview(id,actorId,role,token);await Editable(a,dto.Version,token);
        var note=ReviewNote(dto);
        var person=a.People.SingleOrDefault(p=>p.BookingApplicationPersonId==personId) ?? throw new BookingApplicationNotFoundException();
        if(person.Status!="PendingReview")throw new BookingApplicationConflictException("Hồ sơ người này chưa sẵn sàng hoặc đã được xét duyệt. Hãy tải lại.");
        if(dto.Decision=="Approved" && !BookingApplicationPolicy.HasCompleteIdentity(person))throw new BookingApplicationConflictException("Cần đủ họ tên, CCCD, đồng ý và hai mặt ảnh mới duyệt được.");
        person.Status=dto.Decision;person.ReviewNote=note;person.ReviewedByUserId=actorId;person.ReviewedAtUtc=clock.GetUtcNow().UtcDateTime;person.Version=Guid.NewGuid();
        repository.AddReview(new(){BookingApplicationId=id,PersonId=personId,PersonRevision=person.Revision,Kind="Identity",Decision=dto.Decision,Note=note,
            ReviewerId=actorId,ReviewedAtUtc=person.ReviewedAtUtc.Value,ExpectedApplicationVersion=dto.Version});
        Touch(a);await repository.SaveSubmittedAsync(a,actorId,role,null,token);return await Dto(a,token);
    }
    public async Task<BookingApplicationDto> ReviewRosterAsync(int id,ReviewBookingApplicationDto dto,int actorId,string role,CancellationToken token)
    {
        if(role!=RoleNames.Manager)throw new BookingApplicationNotFoundException();
        var a=await ForReview(id,actorId,role,token);await Editable(a,dto.Version,token);var note=ReviewNote(dto);
        if(a.RosterChangeStatus!="Pending")throw new BookingApplicationConflictException("Không có đề xuất đang chờ xác nhận.");
        if(dto.Decision=="Approved" && (a.People.Count!=a.OccupantCount ||
            !a.People.Select(p=>p.Position).Order().SequenceEqual(Enumerable.Range(0,a.OccupantCount)) ||
            a.People.Any(p=>p.Status!="Approved" || !BookingApplicationPolicy.HasCompleteIdentity(p))))
            throw new BookingApplicationConflictException("Duyệt đủ hồ sơ từng người trước khi xác nhận danh sách mới.");
        a.RosterChangeStatus=dto.Decision;a.RosterChangeNote=note;a.RosterReviewedById=actorId;a.RosterReviewedAtUtc=clock.GetUtcNow().UtcDateTime;
        repository.AddReview(new(){BookingApplicationId=id,Kind="Roster",Decision=dto.Decision,Note=note,ReviewerId=actorId,
            ReviewedAtUtc=a.RosterReviewedAtUtc.Value,ExpectedApplicationVersion=dto.Version});
        Touch(a);await repository.SaveSubmittedAsync(a,actorId,role,dto.Decision=="Approved"?a.OccupantCount:null,token);return await Dto(a,token);
    }
    public async Task<BookingApplicationDto> ResubmitRosterAsync(int id,Guid version,int customerId,CancellationToken token)
    {
        var a=await Owned(id,customerId,token);await Editable(a,version,token);
        if(a.Status!="Submitted" || a.RosterChangeStatus!="Rejected")throw new BookingApplicationConflictException("Chỉ gửi lại đề xuất đã bị từ chối.");
        MarkRosterPending(a);Touch(a);await Persist(a,customerId,token);return await Dto(a,token);
    }
    public async Task<string> ReviewReadImageAsync(int id,int personId,string side,int actorId,string role,CancellationToken token)
    {
        var a=await ForReview(id,actorId,role,token);
        var image=a.People.Where(p=>p.BookingApplicationPersonId==personId).SelectMany(p=>p.Images).SingleOrDefault(i=>i.IsCurrent && i.Side==side)
            ?? throw new BookingApplicationNotFoundException();
        string url;
        try {url=await storage.GetReadUrlAsync(image.ObjectPath,token);}
        catch(Exception ex) when(ex is HttpRequestException or InvalidOperationException){throw new BookingApplicationStorageException();}
        // Do not return a freshly signed URL after a manager was revoked while signing.
        await ForReview(id,actorId,role,token);return url;
    }
}
