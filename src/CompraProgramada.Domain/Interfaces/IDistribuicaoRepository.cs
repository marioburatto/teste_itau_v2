using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Domain.Interfaces;

public interface IDistribuicaoRepository
{
    Task<Distribuicao> AddAsync(Distribuicao distribuicao);
    Task<List<Distribuicao>> GetByClienteIdAsync(int clienteId);
    Task<List<Distribuicao>> GetByDataAsync(DateTime data);
}
