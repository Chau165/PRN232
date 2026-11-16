using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
namespace PRN232.Api.Controllers;
public sealed class BookingIdentityUploadForm
{
    public IFormFile? File { get; set; }
}
[ApiController]
[Route("api/booking-applications")]
[Authorize(Roles="Customer")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class BookingApplicationsController(BookingApplicationService service,ICurrentUserService user) : ControllerBase
{
    [HttpPost] public Task<ActionResult<BookingApplicationDto>> Create(CreateBookingApplicationDto dto,CancellationToken token) =>
        Run(()=>service.CreateAsync(dto,user.UserId!.Value,token));
    [HttpGet("{id:int}")] public Task<ActionResult<BookingApplicationDto>> Get(int id,CancellationToken token) =>
        Run(()=>service.GetAsync(id,user.UserId!.Value,token));
    [HttpPatch("{id:int}")] public Task<ActionResult<BookingApplicationDto>> Update(int id,UpdateBookingApplicationDto dto,CancellationToken token) =>
        Run(()=>service.UpdateAsync(id,dto,user.UserId!.Value,token));
    [HttpPut("{id:int}/people/{personId:int}")] public Task<ActionResult<BookingApplicationDto>> Person(int id,int personId,UpdateBookingPersonDto dto,CancellationToken token) =>
        Run(()=>service.UpdatePersonAsync(id,personId,dto,user.UserId!.Value,token));
    [HttpPost("{id:int}/people/{personId:int}/images/{side}")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(6*1024*1024)]
    [RequestFormLimits(MultipartBodyLengthLimit=6*1024*1024)]
    public Task<ActionResult<BookingApplicationDto>> Upload(int id,int personId,string side,[FromQuery]Guid version,[FromForm]BookingIdentityUploadForm form,CancellationToken token) =>
        Run(async()=> {
            var file=form.File;
            if(file is null || file.Length<=0 || file.Length>5*1024*1024) throw new BookingApplicationValidationException("Ảnh tối đa 5 MiB.");
            using var stream=new MemoryStream();await file.CopyToAsync(stream,token);
            return await service.UploadAsync(id,personId,side,version,new(stream.ToArray(),file.ContentType),user.UserId!.Value,token);
        });
    [HttpDelete("{id:int}/people/{personId:int}/images/{side}")]
    public Task<ActionResult<BookingApplicationDto>> DeleteImage(int id,int personId,string side,[FromQuery]Guid version,CancellationToken token) =>
        Run(()=>service.DeleteImageAsync(id,personId,side,version,user.UserId!.Value,token));
    [HttpPost("{id:int}/people/{personId:int}/images/{side}/read-url")]
    public Task<ActionResult<object>> Read(int id,int personId,string side,CancellationToken token) =>
        Run<object>(async()=>new {url=await service.ReadImageAsync(id,personId,side,user.UserId!.Value,token)});
    [HttpPost("{id:int}/submit")]
    [Microsoft.AspNetCore.RateLimiting.EnableRateLimiting("BookingPolicy")]
    public Task<ActionResult<BookingDto>> Submit(int id,SubmitBookingApplicationDto dto,CancellationToken token) =>
        Run(()=>service.SubmitAsync(id,dto,user.UserId!.Value,token));
    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        if(user.UserId is not >0) return Unauthorized();
        try{return Ok(await action());}
        catch(BookingApplicationNotFoundException){return NotFound(new ProblemDetails{Title="Không tìm thấy hồ sơ."});}
        catch(BookingApplicationValidationException ex){return BadRequest(new ProblemDetails{Title=ex.Message});}
        catch(BookingApplicationConflictException ex){return Conflict(new ProblemDetails{Title=ex.Message});}
        catch(BookingApplicationStorageException){return StatusCode(502,new ProblemDetails{Title="Dịch vụ lưu trữ CCCD hiện không khả dụng."});}
        catch(BookingCustomerHasPendingBookingException ex){return Conflict(new {title=ex.Message,pendingBookingId=ex.PendingBookingId});}
        catch(BookingRoomUnavailableException ex){return Conflict(new ProblemDetails{Title=ex.Message});}
        catch(PhoneNumberRequiredException ex){return BadRequest(new ProblemDetails{Title=ex.Message});}
    }
}
