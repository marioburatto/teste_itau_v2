using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Domain.Interfaces;

public interface ICustodiaFilhoteRepository
{
    Task<List<CustodiaFilhote>> GetByClienteIdAsync(int clienteId);
    Task<CustodiaFilhote?> GetByClienteAndTickerAsync(int clienteId, string ticker);
    Task<CustodiaFilhote> AddAsync(CustodiaFilhote custodia);
    Task UpdateAsync(CustodiaFilhote custodia);
    Task DeleteAsync(CustodiaFilhote custodia);
}
