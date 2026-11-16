using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PRN232.Application.Interfaces;
using PRN232.Application.Services;
namespace PRN232.Api.Controllers;
[ApiController][Authorize(Roles="Customer,Manager,Admin")][Route("api/contracts/{contractId:int}/identity-snapshots")][ResponseCache(NoStore=true,Location=ResponseCacheLocation.None)]
public sealed class ContractIdentityController(ContractIdentityService service,ICurrentUserService user):ControllerBase {
 [HttpGet]public Task<IActionResult> Get(int contractId,CancellationToken token)=>Run(async()=>await service.GetAsync(contractId,user.UserId!.Value,user.Role!,token));
 [HttpPost("{snapshotId:int}/images/{side}/read-url")]public Task<IActionResult> Read(int contractId,int snapshotId,string side,CancellationToken token)=>Run(async()=>new{url=await service.ReadAsync(contractId,snapshotId,side,user.UserId!.Value,user.Role!,token)});
 private async Task<IActionResult> Run(Func<Task<object>> action){
  if(user.UserId is not >0)return Unauthorized();
  try{return Ok(await action());}catch(ContractNotFoundException){return NotFound();}catch(ContractAuthorizationException){return StatusCode(403);}catch(ContractBusinessRuleException ex){return BadRequest(new ProblemDetails{Title=ex.Message});}catch(Exception ex)when(ex is HttpRequestException or InvalidOperationException){return StatusCode(502,new ProblemDetails{Title="Dịch vụ ảnh CCCD hiện không khả dụng."});}
 }
}