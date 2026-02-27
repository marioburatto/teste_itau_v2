using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Domain.Interfaces;

public interface IVendaRebalanceamentoRepository
{
    Task<VendaRebalanceamento> AddAsync(VendaRebalanceamento venda);
    Task<List<VendaRebalanceamento>> GetByClienteAndMesAsync(int clienteId, int ano, int mes);
}
