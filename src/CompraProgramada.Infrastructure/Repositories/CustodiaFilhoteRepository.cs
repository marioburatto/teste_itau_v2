using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class CustodiaFilhoteRepository : ICustodiaFilhoteRepository
{
    private readonly AppDbContext _context;

    public CustodiaFilhoteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CustodiaFilhote>> GetByClienteIdAsync(int clienteId)
    {
        return await _context.CustodiasFilhote
            .Where(c => c.ClienteId == clienteId && c.Quantidade > 0)
            .ToListAsync();
    }

    public async Task<CustodiaFilhote?> GetByClienteAndTickerAsync(int clienteId, string ticker)
    {
        return await _context.CustodiasFilhote
            .FirstOrDefaultAsync(c => c.ClienteId == clienteId && c.Ticker == ticker);
    }

    public async Task<CustodiaFilhote> AddAsync(CustodiaFilhote custodia)
    {
        _context.CustodiasFilhote.Add(custodia);
        await _context.SaveChangesAsync();
        return custodia;
    }

    public async Task UpdateAsync(CustodiaFilhote custodia)
    {
        _context.CustodiasFilhote.Update(custodia);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(CustodiaFilhote custodia)
    {
        _context.CustodiasFilhote.Remove(custodia);
        await _context.SaveChangesAsync();
    }
}
