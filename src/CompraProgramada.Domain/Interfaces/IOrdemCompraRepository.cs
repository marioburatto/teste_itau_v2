using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Domain.Interfaces;

public interface IOrdemCompraRepository
{
    Task<OrdemCompra> AddAsync(OrdemCompra ordem);
    Task<List<OrdemCompra>> GetByDataAsync(DateTime data);
}
