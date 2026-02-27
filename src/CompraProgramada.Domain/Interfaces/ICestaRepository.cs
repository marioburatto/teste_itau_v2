using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Domain.Interfaces;

public interface ICestaRepository
{
    Task<CestaRecomendacao?> GetAtivaAsync();
    Task<CestaRecomendacao?> GetByIdAsync(int id);
    Task<List<CestaRecomendacao>> GetHistoricoAsync();
    Task<CestaRecomendacao> AddAsync(CestaRecomendacao cesta);
    Task UpdateAsync(CestaRecomendacao cesta);
}
