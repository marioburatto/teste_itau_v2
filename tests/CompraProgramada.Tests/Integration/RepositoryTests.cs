using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Enums;
using CompraProgramada.Infrastructure.Data;
using CompraProgramada.Infrastructure.Repositories;
using CompraProgramada.Tests.Helpers;
using FluentAssertions;
using Xunit;

namespace CompraProgramada.Tests.Integration;

public class ClienteRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ClienteRepository _repository;

    public ClienteRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new ClienteRepository(_context);
    }

    [Fact]
    public async Task AddAsync_DeveAdicionarCliente()
    {
        var cliente = new Cliente
        {
            Nome = "Joao da Silva",
            Cpf = "12345678901",
            Email = "joao@email.com",
            ValorMensal = 3000m,
            Ativo = true
        };

        var result = await _repository.AddAsync(cliente);

        result.Id.Should().BeGreaterThan(0);
        result.Nome.Should().Be("Joao da Silva");
    }

    [Fact]
    public async Task GetByIdAsync_DeveRetornarCliente_QuandoExiste()
    {
        var cliente = new Cliente
        {
            Nome = "Maria Souza",
            Cpf = "98765432109",
            Email = "maria@email.com",
            ValorMensal = 6000m,
            Ativo = true,
            ContaGrafica = new ContaGrafica
            {
                NumeroConta = "FLH-000001",
                Tipo = TipoConta.FILHOTE
            }
        };
        await _repository.AddAsync(cliente);

        var result = await _repository.GetByIdAsync(cliente.Id);

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Maria Souza");
        result.ContaGrafica.Should().NotBeNull();
        result.ContaGrafica!.NumeroConta.Should().Be("FLH-000001");
    }

    [Fact]
    public async Task GetByIdAsync_DeveRetornarNull_QuandoNaoExiste()
    {
        var result = await _repository.GetByIdAsync(999);
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByCpfAsync_DeveRetornarCliente_QuandoExiste()
    {
        var cliente = new Cliente
        {
            Nome = "Pedro",
            Cpf = "11122233344",
            Email = "pedro@email.com",
            ValorMensal = 1500m,
            Ativo = true
        };
        await _repository.AddAsync(cliente);

        var result = await _repository.GetByCpfAsync("11122233344");

        result.Should().NotBeNull();
        result!.Nome.Should().Be("Pedro");
    }

    [Fact]
    public async Task GetByCpfAsync_DeveRetornarNull_QuandoNaoExiste()
    {
        var result = await _repository.GetByCpfAsync("00000000000");
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAtivosAsync_DeveRetornarApenasClientesAtivos()
    {
        await _repository.AddAsync(new Cliente { Nome = "Ativo1", Cpf = "111", Email = "a@a.com", ValorMensal = 100m, Ativo = true });
        await _repository.AddAsync(new Cliente { Nome = "Ativo2", Cpf = "222", Email = "b@b.com", ValorMensal = 200m, Ativo = true });
        await _repository.AddAsync(new Cliente { Nome = "Inativo", Cpf = "333", Email = "c@c.com", ValorMensal = 300m, Ativo = false });

        var result = await _repository.GetAtivosAsync();

        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.Ativo);
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarCliente()
    {
        var cliente = new Cliente
        {
            Nome = "Original",
            Cpf = "44455566677",
            Email = "orig@email.com",
            ValorMensal = 1000m,
            Ativo = true
        };
        await _repository.AddAsync(cliente);

        cliente.ValorMensal = 5000m;
        cliente.Ativo = false;
        await _repository.UpdateAsync(cliente);

        var updated = await _repository.GetByIdAsync(cliente.Id);
        updated!.ValorMensal.Should().Be(5000m);
        updated.Ativo.Should().BeFalse();
    }

    public void Dispose() => _context.Dispose();
}

public class CestaRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CestaRepository _repository;

    public CestaRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new CestaRepository(_context);
    }

    [Fact]
    public async Task AddAsync_DeveCriarCestaComItens()
    {
        var cesta = new CestaRecomendacao
        {
            Nome = "Top Five",
            Ativa = true,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };

        var result = await _repository.AddAsync(cesta);

        result.Id.Should().BeGreaterThan(0);
        result.Itens.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetAtivaAsync_DeveRetornarCestaAtiva()
    {
        await _repository.AddAsync(new CestaRecomendacao
        {
            Nome = "Cesta Ativa",
            Ativa = true,
            Itens = new List<CestaItem> { new() { Ticker = "PETR4", Percentual = 100m } }
        });

        var result = await _repository.GetAtivaAsync();

        result.Should().NotBeNull();
        result!.Ativa.Should().BeTrue();
        result.Itens.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetAtivaAsync_DeveRetornarNull_QuandoNenhumaAtiva()
    {
        await _repository.AddAsync(new CestaRecomendacao
        {
            Nome = "Inativa",
            Ativa = false,
            Itens = new List<CestaItem>()
        });

        var result = await _repository.GetAtivaAsync();
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetHistoricoAsync_DeveRetornarTodasCestas()
    {
        await _repository.AddAsync(new CestaRecomendacao { Nome = "C1", Ativa = false, Itens = new List<CestaItem>() });
        await _repository.AddAsync(new CestaRecomendacao { Nome = "C2", Ativa = true, Itens = new List<CestaItem>() });

        var result = await _repository.GetHistoricoAsync();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task UpdateAsync_DeveDesativarCesta()
    {
        var cesta = new CestaRecomendacao { Nome = "Test", Ativa = true, Itens = new List<CestaItem>() };
        await _repository.AddAsync(cesta);

        cesta.Ativa = false;
        cesta.DataDesativacao = DateTime.UtcNow;
        await _repository.UpdateAsync(cesta);

        var updated = await _repository.GetByIdAsync(cesta.Id);
        updated!.Ativa.Should().BeFalse();
        updated.DataDesativacao.Should().NotBeNull();
    }

    public void Dispose() => _context.Dispose();
}

public class CustodiaFilhoteRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CustodiaFilhoteRepository _repository;

    public CustodiaFilhoteRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new CustodiaFilhoteRepository(_context);
    }

    [Fact]
    public async Task AddAsync_DeveCriarCustodia()
    {
        var custodia = new CustodiaFilhote
        {
            ClienteId = 1,
            Ticker = "PETR4",
            Quantidade = 10,
            PrecoMedio = 35m,
            ValorTotalInvestido = 350m
        };

        var result = await _repository.AddAsync(custodia);

        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByClienteIdAsync_DeveRetornarApenasComQuantidade()
    {
        await _repository.AddAsync(new CustodiaFilhote { ClienteId = 1, Ticker = "PETR4", Quantidade = 10, PrecoMedio = 35m });
        await _repository.AddAsync(new CustodiaFilhote { ClienteId = 1, Ticker = "VALE3", Quantidade = 0, PrecoMedio = 62m });

        var result = await _repository.GetByClienteIdAsync(1);

        result.Should().HaveCount(1);
        result.First().Ticker.Should().Be("PETR4");
    }

    [Fact]
    public async Task GetByClienteAndTickerAsync_DeveRetornarCustodia()
    {
        await _repository.AddAsync(new CustodiaFilhote { ClienteId = 1, Ticker = "PETR4", Quantidade = 10, PrecoMedio = 35m });

        var result = await _repository.GetByClienteAndTickerAsync(1, "PETR4");

        result.Should().NotBeNull();
        result!.Quantidade.Should().Be(10);
    }

    [Fact]
    public async Task UpdateAsync_DeveAtualizarQuantidadeEPrecoMedio()
    {
        var custodia = new CustodiaFilhote { ClienteId = 1, Ticker = "PETR4", Quantidade = 10, PrecoMedio = 35m };
        await _repository.AddAsync(custodia);

        custodia.Quantidade = 20;
        custodia.PrecoMedio = 36m;
        await _repository.UpdateAsync(custodia);

        var updated = await _repository.GetByClienteAndTickerAsync(1, "PETR4");
        updated!.Quantidade.Should().Be(20);
        updated.PrecoMedio.Should().Be(36m);
    }

    public void Dispose() => _context.Dispose();
}

public class CustodiaMasterRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly CustodiaMasterRepository _repository;

    public CustodiaMasterRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new CustodiaMasterRepository(_context);
    }

    [Fact]
    public async Task AddAsync_DeveCriarCustodiaMaster()
    {
        var custodia = new CustodiaMaster
        {
            Ticker = "PETR4",
            Quantidade = 3,
            PrecoMedio = 35m,
            Origem = "Residuo test"
        };

        var result = await _repository.AddAsync(custodia);

        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByTickerAsync_DeveRetornarCustodia()
    {
        await _repository.AddAsync(new CustodiaMaster { Ticker = "VALE3", Quantidade = 2, PrecoMedio = 62m });

        var result = await _repository.GetByTickerAsync("VALE3");

        result.Should().NotBeNull();
        result!.Quantidade.Should().Be(2);
    }

    [Fact]
    public async Task GetAllAsync_DeveRetornarTodas()
    {
        await _repository.AddAsync(new CustodiaMaster { Ticker = "PETR4", Quantidade = 1, PrecoMedio = 35m });
        await _repository.AddAsync(new CustodiaMaster { Ticker = "ITUB4", Quantidade = 2, PrecoMedio = 30m });

        var result = await _repository.GetAllAsync();

        result.Should().HaveCount(2);
    }

    public void Dispose() => _context.Dispose();
}

public class ExecucaoCompraRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly ExecucaoCompraRepository _repository;

    public ExecucaoCompraRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new ExecucaoCompraRepository(_context);
    }

    [Fact]
    public async Task AddAsync_DeveCriarExecucao()
    {
        var execucao = new ExecucaoCompra
        {
            DataReferencia = new DateTime(2026, 2, 5),
            TotalClientes = 3,
            TotalConsolidado = 3500m,
            Parcela = "1/3",
            Concluida = true
        };

        var result = await _repository.AddAsync(execucao);

        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByDataReferenciaAsync_DeveRetornar_QuandoExiste()
    {
        await _repository.AddAsync(new ExecucaoCompra
        {
            DataReferencia = new DateTime(2026, 2, 5),
            TotalClientes = 3,
            TotalConsolidado = 3500m,
            Parcela = "1/3",
            Concluida = true
        });

        var result = await _repository.GetByDataReferenciaAsync(new DateTime(2026, 2, 5));

        result.Should().NotBeNull();
        result!.Concluida.Should().BeTrue();
    }

    [Fact]
    public async Task GetByDataReferenciaAsync_DeveRetornarNull_QuandoNaoExiste()
    {
        var result = await _repository.GetByDataReferenciaAsync(new DateTime(2026, 12, 31));
        result.Should().BeNull();
    }

    public void Dispose() => _context.Dispose();
}

public class VendaRebalanceamentoRepositoryTests : IDisposable
{
    private readonly AppDbContext _context;
    private readonly VendaRebalanceamentoRepository _repository;

    public VendaRebalanceamentoRepositoryTests()
    {
        _context = TestDbContextFactory.Create();
        _repository = new VendaRebalanceamentoRepository(_context);
    }

    [Fact]
    public async Task AddAsync_DeveCriarVenda()
    {
        var venda = new VendaRebalanceamento
        {
            ClienteId = 1,
            Ticker = "BBDC4",
            Quantidade = 10,
            PrecoVenda = 15m,
            PrecoMedio = 14m,
            ValorVenda = 150m,
            Lucro = 10m
        };

        var result = await _repository.AddAsync(venda);

        result.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByClienteAndMesAsync_DeveRetornarVendasDoMes()
    {
        var now = DateTime.UtcNow;
        await _repository.AddAsync(new VendaRebalanceamento
        {
            ClienteId = 1, Ticker = "PETR4", Quantidade = 5, PrecoVenda = 40m,
            PrecoMedio = 35m, ValorVenda = 200m, Lucro = 25m, DataVenda = now
        });
        await _repository.AddAsync(new VendaRebalanceamento
        {
            ClienteId = 1, Ticker = "VALE3", Quantidade = 3, PrecoVenda = 65m,
            PrecoMedio = 60m, ValorVenda = 195m, Lucro = 15m, DataVenda = now
        });

        var result = await _repository.GetByClienteAndMesAsync(1, now.Year, now.Month);

        result.Should().HaveCount(2);
    }

    public void Dispose() => _context.Dispose();
}
