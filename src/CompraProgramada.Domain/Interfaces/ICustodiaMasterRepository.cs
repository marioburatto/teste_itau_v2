using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Domain.Interfaces;

public interface ICustodiaMasterRepository
{
    Task<List<CustodiaMaster>> GetAllAsync();
    Task<CustodiaMaster?> GetByTickerAsync(string ticker);
    Task<CustodiaMaster> AddAsync(CustodiaMaster custodia);
    Task UpdateAsync(CustodiaMaster custodia);
}
