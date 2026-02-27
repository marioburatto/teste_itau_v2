using CompraProgramada.Application.Interfaces;
using CompraProgramada.Application.Services;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CompraProgramada.Tests.Unit;

public class RebalanceamentoServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICestaRepository> _cestaRepoMock;
    private readonly Mock<ICustodiaFilhoteRepository> _custodiaFilhoteRepoMock;
    private readonly Mock<IVendaRebalanceamentoRepository> _vendaRepoMock;
    private readonly Mock<ICotahistService> _cotahistMock;
    private readonly MockKafkaProducer _kafkaProducer;
    private readonly RebalanceamentoService _service;

    public RebalanceamentoServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _cestaRepoMock = new Mock<ICestaRepository>();
        _custodiaFilhoteRepoMock = new Mock<ICustodiaFilhoteRepository>();
        _vendaRepoMock = new Mock<IVendaRebalanceamentoRepository>();
        _cotahistMock = new Mock<ICotahistService>();
        _kafkaProducer = new MockKafkaProducer();

        _service = new RebalanceamentoService(
            _clienteRepoMock.Object,
            _cestaRepoMock.Object,
            _custodiaFilhoteRepoMock.Object,
            _vendaRepoMock.Object,
            _cotahistMock.Object,
            _kafkaProducer,
            "cotacoes_test");
    }

    [Fact]
    public async Task RebalancearPorMudancaCesta_DeveVenderAtivosRemovidos()
    {
        var cestaAnterior = new CestaRecomendacao
        {
            Id = 1,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };

        var novaCesta = new CestaRecomendacao
        {
            Id = 2,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 25m },
                new() { Ticker = "VALE3", Percentual = 20m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "ABEV3", Percentual = 20m },
                new() { Ticker = "RENT3", Percentual = 15m }
            }
        };

        var cliente = new Cliente { Id = 1, Nome = "Joao", Cpf = "111", Ativo = true };
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente> { cliente });

        var custodias = new List<CustodiaFilhote>
        {
            new() { ClienteId = 1, Ticker = "PETR4", Quantidade = 8, PrecoMedio = 35m },
            new() { ClienteId = 1, Ticker = "VALE3", Quantidade = 4, PrecoMedio = 62m },
            new() { ClienteId = 1, Ticker = "ITUB4", Quantidade = 6, PrecoMedio = 30m },
            new() { ClienteId = 1, Ticker = "BBDC4", Quantidade = 10, PrecoMedio = 15m },
            new() { ClienteId = 1, Ticker = "WEGE3", Quantidade = 2, PrecoMedio = 40m }
        };
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteIdAsync(1)).ReturnsAsync(custodias);

        var cotacoes = new Dictionary<string, decimal>
        {
            { "PETR4", 35m }, { "VALE3", 62m }, { "ITUB4", 30m },
            { "BBDC4", 15m }, { "WEGE3", 40m }, { "ABEV3", 14m }, { "RENT3", 48m }
        };
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(cotacoes);

        _vendaRepoMock.Setup(r => r.AddAsync(It.IsAny<VendaRebalanceamento>()))
            .ReturnsAsync((VendaRebalanceamento v) => { v.Id = 1; return v; });
        _vendaRepoMock.Setup(r => r.GetByClienteAndMesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<VendaRebalanceamento>());
        _custodiaFilhoteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CustodiaFilhote>())).Returns(Task.CompletedTask);
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);
        _custodiaFilhoteRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaFilhote>()))
            .ReturnsAsync((CustodiaFilhote c) => { c.Id = 1; return c; });

        await _service.RebalancearPorMudancaCestaAsync(cestaAnterior, novaCesta);

        // Verify BBDC4 and WEGE3 were sold (quantity set to 0)
        _vendaRepoMock.Verify(r => r.AddAsync(It.Is<VendaRebalanceamento>(v => v.Ticker == "BBDC4")), Times.Once);
        _vendaRepoMock.Verify(r => r.AddAsync(It.Is<VendaRebalanceamento>(v => v.Ticker == "WEGE3")), Times.Once);

        // Verify new assets were bought
        _custodiaFilhoteRepoMock.Verify(
            r => r.AddAsync(It.Is<CustodiaFilhote>(c => c.Ticker == "ABEV3" && c.Quantidade > 0)), Times.Once);
        _custodiaFilhoteRepoMock.Verify(
            r => r.AddAsync(It.Is<CustodiaFilhote>(c => c.Ticker == "RENT3" && c.Quantidade > 0)), Times.Once);
    }

    [Fact]
    public async Task RebalancearPorMudancaCesta_NaoDeveFazerNada_SemClientesAtivos()
    {
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente>());

        var cestaAnterior = new CestaRecomendacao { Id = 1, Itens = new List<CestaItem>() };
        var novaCesta = new CestaRecomendacao { Id = 2, Itens = new List<CestaItem>() };

        await _service.RebalancearPorMudancaCestaAsync(cestaAnterior, novaCesta);

        _vendaRepoMock.Verify(r => r.AddAsync(It.IsAny<VendaRebalanceamento>()), Times.Never);
    }

    [Fact]
    public async Task RebalancearPorMudancaCesta_DeveCalcularIR_QuandoVendasAcima20k()
    {
        var cestaAnterior = new CestaRecomendacao
        {
            Id = 1,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 50m },
                new() { Ticker = "VALE3", Percentual = 50m },
                new() { Ticker = "ITUB4", Percentual = 0.01m },
                new() { Ticker = "BBDC4", Percentual = 0.01m },
                new() { Ticker = "WEGE3", Percentual = 0.01m }
            }
        };

        var novaCesta = new CestaRecomendacao
        {
            Id = 2,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "ABEV3", Percentual = 50m },
                new() { Ticker = "RENT3", Percentual = 50m },
                new() { Ticker = "ITUB4", Percentual = 0.01m },
                new() { Ticker = "BBDC4", Percentual = 0.01m },
                new() { Ticker = "WEGE3", Percentual = 0.01m }
            }
        };

        var cliente = new Cliente { Id = 1, Nome = "Big Investor", Cpf = "111", Ativo = true };
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente> { cliente });

        // Big positions: 500 shares at 50 = 25000 in sales
        var custodias = new List<CustodiaFilhote>
        {
            new() { ClienteId = 1, Ticker = "PETR4", Quantidade = 500, PrecoMedio = 30m },
            new() { ClienteId = 1, Ticker = "VALE3", Quantidade = 300, PrecoMedio = 40m }
        };
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteIdAsync(1)).ReturnsAsync(custodias);

        var cotacoes = new Dictionary<string, decimal>
        {
            { "PETR4", 50m }, { "VALE3", 60m },
            { "ABEV3", 14m }, { "RENT3", 48m },
            { "ITUB4", 30m }, { "BBDC4", 15m }, { "WEGE3", 40m }
        };
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(cotacoes);

        _vendaRepoMock.Setup(r => r.AddAsync(It.IsAny<VendaRebalanceamento>()))
            .ReturnsAsync((VendaRebalanceamento v) => { v.Id = 1; return v; });
        _vendaRepoMock.Setup(r => r.GetByClienteAndMesAsync(It.IsAny<int>(), It.IsAny<int>(), It.IsAny<int>()))
            .ReturnsAsync(new List<VendaRebalanceamento>());
        _custodiaFilhoteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CustodiaFilhote>())).Returns(Task.CompletedTask);
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);
        _custodiaFilhoteRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaFilhote>()))
            .ReturnsAsync((CustodiaFilhote c) => { c.Id = 1; return c; });

        await _service.RebalancearPorMudancaCestaAsync(cestaAnterior, novaCesta);

        // Sales: PETR4 500*50=25000, VALE3 300*60=18000 => total 43000 > 20000
        // Lucro PETR4: (50-30)*500=10000, Lucro VALE3: (60-40)*300=6000 => total 16000
        // IR: 16000 * 20% = 3200
        _kafkaProducer.MensagensIRVenda.Should().NotBeEmpty();
    }

    [Fact]
    public async Task RebalancearPorDesvioProporcao_NaoDeveFazerNada_SemCestaAtiva()
    {
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync((CestaRecomendacao?)null);

        await _service.RebalancearPorDesvioProporcaoAsync();

        _vendaRepoMock.Verify(r => r.AddAsync(It.IsAny<VendaRebalanceamento>()), Times.Never);
    }

    [Fact]
    public async Task RebalancearPorDesvioProporcao_NaoDeveFazerNada_SemDesvioSignificativo()
    {
        var cesta = new CestaRecomendacao
        {
            Id = 1,
            Ativa = true,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 50m },
                new() { Ticker = "VALE3", Percentual = 50m },
                new() { Ticker = "ITUB4", Percentual = 0.01m },
                new() { Ticker = "BBDC4", Percentual = 0.01m },
                new() { Ticker = "WEGE3", Percentual = 0.01m }
            }
        };
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(cesta);

        var cliente = new Cliente { Id = 1, Nome = "Joao", Ativo = true };
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente> { cliente });

        // Portfolio balanced: 50%/50%
        var custodias = new List<CustodiaFilhote>
        {
            new() { ClienteId = 1, Ticker = "PETR4", Quantidade = 10, PrecoMedio = 35m },
            new() { ClienteId = 1, Ticker = "VALE3", Quantidade = 5, PrecoMedio = 70m }
        };
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteIdAsync(1)).ReturnsAsync(custodias);

        // PETR4: 10*35=350 (50%), VALE3: 5*70=350 (50%) => balanced
        var cotacoes = new Dictionary<string, decimal>
        {
            { "PETR4", 35m }, { "VALE3", 70m },
            { "ITUB4", 30m }, { "BBDC4", 15m }, { "WEGE3", 40m }
        };
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(cotacoes);

        await _service.RebalancearPorDesvioProporcaoAsync();

        // No deviation > 5pp, so no sells
        _vendaRepoMock.Verify(r => r.AddAsync(It.IsAny<VendaRebalanceamento>()), Times.Never);
    }
}
