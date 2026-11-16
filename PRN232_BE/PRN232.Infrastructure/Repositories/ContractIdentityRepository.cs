using Microsoft.EntityFrameworkCore;
using PRN232.Application.Interfaces;
using PRN232.Infrastructure.Data;
using PRN232.Domain.Entities;
namespace PRN232.Infrastructure.Repositories;
public sealed class ContractIdentityRepository(AppDbContext db):IContractIdentityRepository
{
    public Task<List<ContractIdentitySnapshot>> GetAsync(int id,CancellationToken token)=>db.ContractIdentitySnapshots.AsNoTracking().Where(s=>s.ContractId==id).OrderBy(s=>s.Position).ToListAsync(token);
}
