using PRN232.Application.Interfaces;
using PRN232.Application.Policies;
namespace PRN232.Application.Services;
public record ContractIdentitySnapshotDto(int SnapshotId,int Position,string FullName,string MaskedIdentityNumber,int SourceRevision,DateTime CreatedAtUtc);
public sealed class ContractIdentityService(IContractService contracts,IContractIdentityRepository repository,IBookingIdentityImageStorage storage)
{
    private async Task Authorize(int id,int actorId,string role) {if(await contracts.GetByIdAsync(id,actorId,role) is null)throw new ContractNotFoundException("Không tìm thấy hợp đồng.");}
    public async Task<List<ContractIdentitySnapshotDto>> GetAsync(int id,int actorId,string role,CancellationToken token)
    {
        await Authorize(id,actorId,role);var rows=await repository.GetAsync(id,token);
        return rows.Select(s=>new ContractIdentitySnapshotDto(s.ContractIdentitySnapshotId,s.Position,s.FullName,CccdMasking.MaskNumber(s.IdentityNumber),s.SourceRevision,s.CreatedAtUtc)).ToList();
    }
    public async Task<string> ReadAsync(int id,int snapshotId,string side,int actorId,string role,CancellationToken token)
    {
        await Authorize(id,actorId,role);var row=(await repository.GetAsync(id,token)).SingleOrDefault(s=>s.ContractIdentitySnapshotId==snapshotId)??throw new ContractNotFoundException("Không tìm thấy hồ sơ đã chốt.");
        var path=side switch{"Front"=>row.FrontObjectPath,"Back"=>row.BackObjectPath,_=>throw new ContractBusinessRuleException("Mặt ảnh không hợp lệ.")};
        var url=await storage.GetReadUrlAsync(path,token);await Authorize(id,actorId,role);return url;
    }
}
