using PRN232.Domain.Entities;
namespace PRN232.Application.Interfaces;
public interface IContractIdentityRepository
{
    Task<List<ContractIdentitySnapshot>> GetAsync(int contractId,CancellationToken token);
}
