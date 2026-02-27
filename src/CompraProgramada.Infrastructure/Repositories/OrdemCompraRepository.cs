using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class OrdemCompraRepository : IOrdemCompraRepository
{
    private readonly AppDbContext _context;

    public OrdemCompraRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<OrdemCompra> AddAsync(OrdemCompra ordem)
    {
        _context.OrdensCompra.Add(ordem);
        await _context.SaveChangesAsync();
        return ordem;
    }

    public async Task<List<OrdemCompra>> GetByDataAsync(DateTime data)
    {
        return await _context.OrdensCompra
            .Where(o => o.DataExecucao.Date == data.Date)
            .ToListAsync();
    }
}
