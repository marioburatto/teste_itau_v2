using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Domain.Interfaces;

public interface IExecucaoCompraRepository
{
    Task<ExecucaoCompra> AddAsync(ExecucaoCompra execucao);
    Task<ExecucaoCompra?> GetByDataReferenciaAsync(DateTime dataReferencia);
    Task UpdateAsync(ExecucaoCompra execucao);
}
