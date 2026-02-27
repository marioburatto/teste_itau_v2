using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class DistribuicaoRepository : IDistribuicaoRepository
{
    private readonly AppDbContext _context;

    public DistribuicaoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Distribuicao> AddAsync(Distribuicao distribuicao)
    {
        _context.Distribuicoes.Add(distribuicao);
        await _context.SaveChangesAsync();
        return distribuicao;
    }

    public async Task<List<Distribuicao>> GetByClienteIdAsync(int clienteId)
    {
        return await _context.Distribuicoes
            .Where(d => d.ClienteId == clienteId)
            .OrderByDescending(d => d.DataDistribuicao)
            .ToListAsync();
    }

    public async Task<List<Distribuicao>> GetByDataAsync(DateTime data)
    {
        return await _context.Distribuicoes
            .Where(d => d.DataDistribuicao.Date == data.Date)
            .ToListAsync();
    }
}
