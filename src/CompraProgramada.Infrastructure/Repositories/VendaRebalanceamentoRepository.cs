using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class VendaRebalanceamentoRepository : IVendaRebalanceamentoRepository
{
    private readonly AppDbContext _context;

    public VendaRebalanceamentoRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<VendaRebalanceamento> AddAsync(VendaRebalanceamento venda)
    {
        _context.VendasRebalanceamento.Add(venda);
        await _context.SaveChangesAsync();
        return venda;
    }

    public async Task<List<VendaRebalanceamento>> GetByClienteAndMesAsync(int clienteId, int ano, int mes)
    {
        return await _context.VendasRebalanceamento
            .Where(v => v.ClienteId == clienteId && v.DataVenda.Year == ano && v.DataVenda.Month == mes)
            .ToListAsync();
    }
}
