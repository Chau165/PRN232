using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.DTOs;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
namespace PRN232.Api.Controllers;
[ApiController]
[Route("api/booking-application-reviews")]
[Authorize(Roles="Admin,Manager")]
[ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class BookingApplicationReviewsController(BookingApplicationService service,ICurrentUserService user):ControllerBase
{
    [HttpGet] public Task<ActionResult<List<BookingApplicationDto>>> List([FromQuery]int page=1,CancellationToken token=default)=>Run(()=>service.ReviewListAsync(user.UserId!.Value,user.Role!,page,token));
    [HttpGet("by-booking/{bookingId:int}/contract-source")][Authorize(Roles="Manager")]
    public Task<ActionResult<BookingApplicationDto>> Source(int bookingId,CancellationToken token)=>Run(()=>service.ContractSourceAsync(bookingId,user.UserId!.Value,user.Role!,token));
    [HttpGet("{id:int}")] public Task<ActionResult<BookingApplicationDto>> Get(int id,CancellationToken token)=>Run(()=>service.GetReviewAsync(id,user.UserId!.Value,user.Role!,token));
    [HttpPost("{id:int}/people/{personId:int}/review")]
    public Task<ActionResult<BookingApplicationDto>> Review(int id,int personId,ReviewBookingApplicationDto dto,CancellationToken token)=>Run(()=>service.ReviewPersonAsync(id,personId,dto,user.UserId!.Value,user.Role!,token));
    [HttpPost("{id:int}/roster-review")][Authorize(Roles="Manager")]
    public Task<ActionResult<BookingApplicationDto>> Roster(int id,ReviewBookingApplicationDto dto,CancellationToken token)=>Run(()=>service.ReviewRosterAsync(id,dto,user.UserId!.Value,user.Role!,token));
    [HttpPost("{id:int}/people/{personId:int}/images/{side}/read-url")]
    public Task<ActionResult<object>> Read(int id,int personId,string side,CancellationToken token)=>Run<object>(async()=>new{url=await service.ReviewReadImageAsync(id,personId,side,user.UserId!.Value,user.Role!,token)});
    private async Task<ActionResult<T>> Run<T>(Func<Task<T>> action)
    {
        if(user.UserId is not >0)return Unauthorized();
        try{return Ok(await action());}
        catch(BookingApplicationNotFoundException){return NotFound(new ProblemDetails{Title="Không tìm thấy hồ sơ trong phạm vi quản lý."});}
        catch(BookingApplicationValidationException ex){return BadRequest(new ProblemDetails{Title=ex.Message});}
        catch(BookingApplicationConflictException ex){return Conflict(new ProblemDetails{Title=ex.Message});}
        catch(BookingApplicationStorageException){return StatusCode(502,new ProblemDetails{Title="Dịch vụ ảnh CCCD hiện không khả dụng."});}
    }
}
