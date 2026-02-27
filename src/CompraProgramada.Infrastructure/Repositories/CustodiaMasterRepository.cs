using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class CustodiaMasterRepository : ICustodiaMasterRepository
{
    private readonly AppDbContext _context;

    public CustodiaMasterRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<CustodiaMaster>> GetAllAsync()
    {
        return await _context.CustodiasMaster.ToListAsync();
    }

    public async Task<CustodiaMaster?> GetByTickerAsync(string ticker)
    {
        return await _context.CustodiasMaster.FirstOrDefaultAsync(c => c.Ticker == ticker);
    }

    public async Task<CustodiaMaster> AddAsync(CustodiaMaster custodia)
    {
        _context.CustodiasMaster.Add(custodia);
        await _context.SaveChangesAsync();
        return custodia;
    }

    public async Task UpdateAsync(CustodiaMaster custodia)
    {
        _context.CustodiasMaster.Update(custodia);
        await _context.SaveChangesAsync();
    }
}
