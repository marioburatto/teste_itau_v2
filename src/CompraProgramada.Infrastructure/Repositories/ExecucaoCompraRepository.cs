using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class ExecucaoCompraRepository : IExecucaoCompraRepository
{
    private readonly AppDbContext _context;

    public ExecucaoCompraRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ExecucaoCompra> AddAsync(ExecucaoCompra execucao)
    {
        _context.ExecucoesCompra.Add(execucao);
        await _context.SaveChangesAsync();
        return execucao;
    }

    public async Task<ExecucaoCompra?> GetByDataReferenciaAsync(DateTime dataReferencia)
    {
        return await _context.ExecucoesCompra
            .FirstOrDefaultAsync(e => e.DataReferencia.Date == dataReferencia.Date);
    }

    public async Task UpdateAsync(ExecucaoCompra execucao)
    {
        _context.ExecucoesCompra.Update(execucao);
        await _context.SaveChangesAsync();
    }
}
