using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class CestaRepository : ICestaRepository
{
    private readonly AppDbContext _context;

    public CestaRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<CestaRecomendacao?> GetAtivaAsync()
    {
        return await _context.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Ativa);
    }

    public async Task<CestaRecomendacao?> GetByIdAsync(int id)
    {
        return await _context.CestasRecomendacao
            .Include(c => c.Itens)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<List<CestaRecomendacao>> GetHistoricoAsync()
    {
        return await _context.CestasRecomendacao
            .Include(c => c.Itens)
            .OrderByDescending(c => c.DataCriacao)
            .ToListAsync();
    }

    public async Task<CestaRecomendacao> AddAsync(CestaRecomendacao cesta)
    {
        _context.CestasRecomendacao.Add(cesta);
        await _context.SaveChangesAsync();
        return cesta;
    }

    public async Task UpdateAsync(CestaRecomendacao cesta)
    {
        _context.CestasRecomendacao.Update(cesta);
        await _context.SaveChangesAsync();
    }
}
