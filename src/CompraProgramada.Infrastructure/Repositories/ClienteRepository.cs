using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CompraProgramada.Infrastructure.Repositories;

public class ClienteRepository : IClienteRepository
{
    private readonly AppDbContext _context;

    public ClienteRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<Cliente?> GetByIdAsync(int id)
    {
        return await _context.Clientes
            .Include(c => c.ContaGrafica)
            .Include(c => c.CustodiaFilhote)
            .Include(c => c.HistoricoAportes)
            .Include(c => c.HistoricoValoresMensais)
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    public async Task<Cliente?> GetByCpfAsync(string cpf)
    {
        return await _context.Clientes
            .Include(c => c.ContaGrafica)
            .FirstOrDefaultAsync(c => c.Cpf == cpf);
    }

    public async Task<List<Cliente>> GetAtivosAsync()
    {
        return await _context.Clientes
            .Include(c => c.ContaGrafica)
            .Include(c => c.HistoricoAportes)
            .Where(c => c.Ativo)
            .ToListAsync();
    }

    public async Task<Cliente> AddAsync(Cliente cliente)
    {
        _context.Clientes.Add(cliente);
        await _context.SaveChangesAsync();
        return cliente;
    }

    public async Task UpdateAsync(Cliente cliente)
    {
        _context.Clientes.Update(cliente);
        await _context.SaveChangesAsync();
    }
}
