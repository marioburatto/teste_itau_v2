using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Interfaces;
using CompraProgramada.Application.Services;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using CompraProgramada.Tests.Helpers;
using FluentAssertions;
using Moq;
using Xunit;

namespace CompraProgramada.Tests.Unit;

public class MotorCompraServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICestaRepository> _cestaRepoMock;
    private readonly Mock<ICustodiaFilhoteRepository> _custodiaFilhoteRepoMock;
    private readonly Mock<ICustodiaMasterRepository> _custodiaMasterRepoMock;
    private readonly Mock<IOrdemCompraRepository> _ordemCompraRepoMock;
    private readonly Mock<IDistribuicaoRepository> _distribuicaoRepoMock;
    private readonly Mock<IExecucaoCompraRepository> _execucaoCompraRepoMock;
    private readonly Mock<ICotahistService> _cotahistMock;
    private readonly MockKafkaProducer _kafkaProducer;
    private readonly MotorCompraService _service;

    public MotorCompraServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _cestaRepoMock = new Mock<ICestaRepository>();
        _custodiaFilhoteRepoMock = new Mock<ICustodiaFilhoteRepository>();
        _custodiaMasterRepoMock = new Mock<ICustodiaMasterRepository>();
        _ordemCompraRepoMock = new Mock<IOrdemCompraRepository>();
        _distribuicaoRepoMock = new Mock<IDistribuicaoRepository>();
        _execucaoCompraRepoMock = new Mock<IExecucaoCompraRepository>();
        _cotahistMock = new Mock<ICotahistService>();
        _kafkaProducer = new MockKafkaProducer();

        _service = new MotorCompraService(
            _clienteRepoMock.Object,
            _cestaRepoMock.Object,
            _custodiaFilhoteRepoMock.Object,
            _custodiaMasterRepoMock.Object,
            _ordemCompraRepoMock.Object,
            _distribuicaoRepoMock.Object,
            _execucaoCompraRepoMock.Object,
            _cotahistMock.Object,
            _kafkaProducer,
            "cotacoes_test");
    }

    [Theory]
    [InlineData(2026, 2, 5, 2026, 2, 5)]   // Thursday -> Thursday
    [InlineData(2026, 2, 15, 2026, 2, 16)]  // Sunday -> Monday
    [InlineData(2026, 2, 25, 2026, 2, 25)]  // Wednesday -> Wednesday
    public void ObterProximaDataCompra_DeveRetornarDataCorreta(
        int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        var data = new DateTime(year, month, day);
        var expected = new DateTime(expectedYear, expectedMonth, expectedDay);
        var result = MotorCompraService.ObterProximaDataCompra(data);

        result.Should().Be(expected);
    }

    [Theory]
    [InlineData(5, "1/3")]
    [InlineData(7, "1/3")]
    [InlineData(10, "1/3")]
    [InlineData(15, "2/3")]
    [InlineData(17, "2/3")]
    [InlineData(25, "3/3")]
    [InlineData(28, "3/3")]
    public void DeterminarParcela_DeveRetornarParcelaCorreta(int dia, string parcelaEsperada)
    {
        var data = new DateTime(2026, 2, dia);
        var result = MotorCompraService.DeterminarParcela(data);
        result.Should().Be(parcelaEsperada);
    }

    [Fact]
    public async Task ExecutarCompra_DeveLancarExcecao_QuandoJaExecutada()
    {
        var dataRef = new DateTime(2026, 2, 5);
        _execucaoCompraRepoMock.Setup(r => r.GetByDataReferenciaAsync(dataRef.Date))
            .ReturnsAsync(new ExecucaoCompra { Concluida = true });

        var act = () => _service.ExecutarCompraAsync(dataRef);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("COMPRA_JA_EXECUTADA");
    }

    [Fact]
    public async Task ExecutarCompra_DeveLancarExcecao_QuandoNenhumaCestaAtiva()
    {
        var dataRef = new DateTime(2026, 2, 5);
        _execucaoCompraRepoMock.Setup(r => r.GetByDataReferenciaAsync(dataRef.Date))
            .ReturnsAsync((ExecucaoCompra?)null);
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync((CestaRecomendacao?)null);

        var act = () => _service.ExecutarCompraAsync(dataRef);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be("CESTA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task ExecutarCompra_DeveLancarExcecao_QuandoSemClientesAtivos()
    {
        var dataRef = new DateTime(2026, 2, 5);
        _execucaoCompraRepoMock.Setup(r => r.GetByDataReferenciaAsync(dataRef.Date))
            .ReturnsAsync((ExecucaoCompra?)null);
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(new CestaRecomendacao
        {
            Id = 1, Ativa = true,
            Itens = new List<CestaItem> { new() { Ticker = "PETR4", Percentual = 100m } }
        });
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente>());

        var act = () => _service.ExecutarCompraAsync(dataRef);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("SEM_CLIENTES_ATIVOS");
    }

    [Fact]
    public async Task ExecutarCompra_DeveCalcularCorreto_CenarioCompleto()
    {
        var dataRef = new DateTime(2026, 2, 5);

        _execucaoCompraRepoMock.Setup(r => r.GetByDataReferenciaAsync(dataRef.Date))
            .ReturnsAsync((ExecucaoCompra?)null);

        var cesta = new CestaRecomendacao
        {
            Id = 1, Ativa = true,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(cesta);

        var clienteA = new Cliente { Id = 1, Nome = "Cliente A", Cpf = "11111111111", ValorMensal = 3000m, Ativo = true, HistoricoAportes = new List<HistoricoAporte>() };
        var clienteB = new Cliente { Id = 2, Nome = "Cliente B", Cpf = "22222222222", ValorMensal = 6000m, Ativo = true, HistoricoAportes = new List<HistoricoAporte>() };
        var clienteC = new Cliente { Id = 3, Nome = "Cliente C", Cpf = "33333333333", ValorMensal = 1500m, Ativo = true, HistoricoAportes = new List<HistoricoAporte>() };

        _clienteRepoMock.Setup(r => r.GetAtivosAsync())
            .ReturnsAsync(new List<Cliente> { clienteA, clienteB, clienteC });

        var cotacoes = new Dictionary<string, decimal>
        {
            { "PETR4", 35m }, { "VALE3", 62m }, { "ITUB4", 30m },
            { "BBDC4", 15m }, { "WEGE3", 40m }
        };
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(cotacoes);

        // Master custody has residuals: PETR4=2, ITUB4=1
        _custodiaMasterRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CustodiaMaster>
        {
            new() { Ticker = "PETR4", Quantidade = 2, PrecoMedio = 34m },
            new() { Ticker = "ITUB4", Quantidade = 1, PrecoMedio = 29m }
        });

        _custodiaMasterRepoMock.Setup(r => r.GetByTickerAsync(It.IsAny<string>()))
            .ReturnsAsync((string ticker) =>
            {
                if (ticker == "PETR4") return new CustodiaMaster { Ticker = "PETR4", Quantidade = 0, PrecoMedio = 34m };
                if (ticker == "ITUB4") return new CustodiaMaster { Ticker = "ITUB4", Quantidade = 0, PrecoMedio = 29m };
                return null;
            });

        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);

        _ordemCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<OrdemCompra>()))
            .ReturnsAsync((OrdemCompra o) => { o.Id = 1; return o; });
        _distribuicaoRepoMock.Setup(r => r.AddAsync(It.IsAny<Distribuicao>()))
            .ReturnsAsync((Distribuicao d) => { d.Id = 1; return d; });
        _custodiaFilhoteRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaFilhote>()))
            .ReturnsAsync((CustodiaFilhote c) => { c.Id = 1; return c; });
        _custodiaMasterRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaMaster>()))
            .ReturnsAsync((CustodiaMaster c) => { c.Id = 1; return c; });
        _custodiaMasterRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CustodiaMaster>())).Returns(Task.CompletedTask);
        _execucaoCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<ExecucaoCompra>()))
            .ReturnsAsync((ExecucaoCompra e) => { e.Id = 1; return e; });
        _clienteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        var result = await _service.ExecutarCompraAsync(dataRef);

        result.Should().NotBeNull();
        result.TotalClientes.Should().Be(3);

        // Total: 1000 + 2000 + 500 = 3500
        result.TotalConsolidado.Should().Be(3500m);

        result.OrdensCompra.Should().NotBeEmpty();
        result.Distribuicoes.Should().HaveCount(3);

        // PETR4: 3500 * 30% = 1050 / 35 = 30 - 2 (master) = 28 to buy
        var ordemPetr4 = result.OrdensCompra.FirstOrDefault(o => o.Ticker == "PETR4");
        ordemPetr4.Should().NotBeNull();
        ordemPetr4!.QuantidadeTotal.Should().Be(28);

        // VALE3: 3500 * 25% = 875 / 62 = 14 - 0 = 14 to buy
        var ordemVale3 = result.OrdensCompra.FirstOrDefault(o => o.Ticker == "VALE3");
        ordemVale3.Should().NotBeNull();
        ordemVale3!.QuantidadeTotal.Should().Be(14);

        // ITUB4: 3500 * 20% = 700 / 30 = 23 - 1 (master) = 22 to buy
        var ordemItub4 = result.OrdensCompra.FirstOrDefault(o => o.Ticker == "ITUB4");
        ordemItub4.Should().NotBeNull();
        ordemItub4!.QuantidadeTotal.Should().Be(22);

        // BBDC4: 3500 * 15% = 525 / 15 = 35 - 0 = 35 to buy
        var ordemBbdc4 = result.OrdensCompra.FirstOrDefault(o => o.Ticker == "BBDC4");
        ordemBbdc4.Should().NotBeNull();
        ordemBbdc4!.QuantidadeTotal.Should().Be(35);

        // WEGE3: 3500 * 10% = 350 / 40 = 8 - 0 = 8 to buy
        var ordemWege3 = result.OrdensCompra.FirstOrDefault(o => o.Ticker == "WEGE3");
        ordemWege3.Should().NotBeNull();
        ordemWege3!.QuantidadeTotal.Should().Be(8);

        // Kafka events should have been published
        _kafkaProducer.MensagensIRDedoDuro.Should().NotBeEmpty();

        result.Mensagem.Should().Contain("3 clientes");
    }

    [Fact]
    public async Task ExecutarCompra_DeveCalcularDistribuicaoProporcional()
    {
        var dataRef = new DateTime(2026, 2, 5);

        _execucaoCompraRepoMock.Setup(r => r.GetByDataReferenciaAsync(dataRef.Date))
            .ReturnsAsync((ExecucaoCompra?)null);

        var cesta = new CestaRecomendacao
        {
            Id = 1, Ativa = true,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 100m }
            }
        };
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(cesta);

        var clienteA = new Cliente { Id = 1, Nome = "A", Cpf = "111", ValorMensal = 3000m, Ativo = true, HistoricoAportes = new() };
        var clienteB = new Cliente { Id = 2, Nome = "B", Cpf = "222", ValorMensal = 6000m, Ativo = true, HistoricoAportes = new() };

        _clienteRepoMock.Setup(r => r.GetAtivosAsync())
            .ReturnsAsync(new List<Cliente> { clienteA, clienteB });

        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new Dictionary<string, decimal> { { "PETR4", 10m } });

        _custodiaMasterRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CustodiaMaster>());
        _custodiaMasterRepoMock.Setup(r => r.GetByTickerAsync(It.IsAny<string>())).ReturnsAsync((CustodiaMaster?)null);
        _custodiaMasterRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaMaster>()))
            .ReturnsAsync((CustodiaMaster c) => { c.Id = 1; return c; });
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);
        _custodiaFilhoteRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaFilhote>()))
            .ReturnsAsync((CustodiaFilhote c) => { c.Id = 1; return c; });
        _ordemCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<OrdemCompra>()))
            .ReturnsAsync((OrdemCompra o) => { o.Id = 1; return o; });
        _distribuicaoRepoMock.Setup(r => r.AddAsync(It.IsAny<Distribuicao>()))
            .ReturnsAsync((Distribuicao d) => { d.Id = 1; return d; });
        _execucaoCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<ExecucaoCompra>()))
            .ReturnsAsync((ExecucaoCompra e) => { e.Id = 1; return e; });
        _clienteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        var result = await _service.ExecutarCompraAsync(dataRef);

        // Total: 1000 + 2000 = 3000 -> 300 shares at 10
        // A: 33.33% -> TRUNCAR(300*0.3333) = 99
        // B: 66.67% -> TRUNCAR(300*0.6667) = 200
        // Total distributed: 299, residual: 1

        var distA = result.Distribuicoes.First(d => d.ClienteId == 1);
        var distB = result.Distribuicoes.First(d => d.ClienteId == 2);

        distA.Ativos.First().Quantidade.Should().Be(99);
        distB.Ativos.First().Quantidade.Should().Be(200);
    }

    [Fact]
    public async Task ExecutarCompra_DeveSepararLotePadraoEFracionario()
    {
        var dataRef = new DateTime(2026, 2, 5);

        _execucaoCompraRepoMock.Setup(r => r.GetByDataReferenciaAsync(dataRef.Date))
            .ReturnsAsync((ExecucaoCompra?)null);

        var cesta = new CestaRecomendacao
        {
            Id = 1, Ativa = true,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 100m }
            }
        };
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(cesta);

        var cliente = new Cliente { Id = 1, Nome = "Big", Cpf = "111", ValorMensal = 300000m, Ativo = true, HistoricoAportes = new() };
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente> { cliente });

        // 100000 / 10 = 10000 shares -> 350 shares to buy
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new Dictionary<string, decimal> { { "PETR4", 2.857m } }); // 100000/2.857 = ~35000+ shares

        _custodiaMasterRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CustodiaMaster>());
        _custodiaMasterRepoMock.Setup(r => r.GetByTickerAsync(It.IsAny<string>())).ReturnsAsync((CustodiaMaster?)null);
        _custodiaMasterRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaMaster>()))
            .ReturnsAsync((CustodiaMaster c) => { c.Id = 1; return c; });
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);
        _custodiaFilhoteRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaFilhote>()))
            .ReturnsAsync((CustodiaFilhote c) => { c.Id = 1; return c; });
        _ordemCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<OrdemCompra>()))
            .ReturnsAsync((OrdemCompra o) => { o.Id = 1; return o; });
        _distribuicaoRepoMock.Setup(r => r.AddAsync(It.IsAny<Distribuicao>()))
            .ReturnsAsync((Distribuicao d) => { d.Id = 1; return d; });
        _execucaoCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<ExecucaoCompra>()))
            .ReturnsAsync((ExecucaoCompra e) => { e.Id = 1; return e; });
        _clienteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        var result = await _service.ExecutarCompraAsync(dataRef);

        // Check that lot standard and fractional are separated
        var ordem = result.OrdensCompra.First();
        var total = ordem.QuantidadeTotal;

        var lotePadrao = ordem.Detalhes.Where(d => d.Tipo == "LOTE_PADRAO").Sum(d => d.Quantidade);
        var fracionario = ordem.Detalhes.Where(d => d.Tipo == "FRACIONARIO").Sum(d => d.Quantidade);

        (lotePadrao + fracionario).Should().Be(total);
        (lotePadrao % 100).Should().Be(0);
        fracionario.Should().BeLessThan(100);

        // Fractional ticker should end with F
        var fracDetail = ordem.Detalhes.FirstOrDefault(d => d.Tipo == "FRACIONARIO");
        if (fracDetail != null)
        {
            fracDetail.Ticker.Should().EndWith("F");
        }
    }

    [Fact]
    public async Task ExecutarCompra_DevePublicarIRDedoDuro()
    {
        var dataRef = new DateTime(2026, 2, 5);

        _execucaoCompraRepoMock.Setup(r => r.GetByDataReferenciaAsync(dataRef.Date))
            .ReturnsAsync((ExecucaoCompra?)null);

        var cesta = new CestaRecomendacao
        {
            Id = 1, Ativa = true,
            Itens = new List<CestaItem>
            {
                new() { Ticker = "PETR4", Percentual = 100m }
            }
        };
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(cesta);

        var cliente = new Cliente { Id = 1, Nome = "A", Cpf = "111", ValorMensal = 3000m, Ativo = true, HistoricoAportes = new() };
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente> { cliente });

        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new Dictionary<string, decimal> { { "PETR4", 35m } });

        _custodiaMasterRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CustodiaMaster>());
        _custodiaMasterRepoMock.Setup(r => r.GetByTickerAsync(It.IsAny<string>())).ReturnsAsync((CustodiaMaster?)null);
        _custodiaMasterRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaMaster>()))
            .ReturnsAsync((CustodiaMaster c) => { c.Id = 1; return c; });
        _custodiaFilhoteRepoMock.Setup(r => r.GetByClienteAndTickerAsync(It.IsAny<int>(), It.IsAny<string>()))
            .ReturnsAsync((CustodiaFilhote?)null);
        _custodiaFilhoteRepoMock.Setup(r => r.AddAsync(It.IsAny<CustodiaFilhote>()))
            .ReturnsAsync((CustodiaFilhote c) => { c.Id = 1; return c; });
        _ordemCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<OrdemCompra>()))
            .ReturnsAsync((OrdemCompra o) => { o.Id = 1; return o; });
        _distribuicaoRepoMock.Setup(r => r.AddAsync(It.IsAny<Distribuicao>()))
            .ReturnsAsync((Distribuicao d) => { d.Id = 1; return d; });
        _execucaoCompraRepoMock.Setup(r => r.AddAsync(It.IsAny<ExecucaoCompra>()))
            .ReturnsAsync((ExecucaoCompra e) => { e.Id = 1; return e; });
        _clienteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        var result = await _service.ExecutarCompraAsync(dataRef);

        _kafkaProducer.MensagensIRDedoDuro.Should().NotBeEmpty();
        result.EventosIRPublicados.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task ConsultarCustodiaMaster_DeveRetornarCustodias()
    {
        _custodiaMasterRepoMock.Setup(r => r.GetAllAsync()).ReturnsAsync(new List<CustodiaMaster>
        {
            new() { Ticker = "PETR4", Quantidade = 3, PrecoMedio = 35m, Origem = "Residuo 2026-02-05" },
            new() { Ticker = "ITUB4", Quantidade = 1, PrecoMedio = 30m, Origem = "Residuo 2026-02-05" }
        });

        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new Dictionary<string, decimal> { { "PETR4", 37m }, { "ITUB4", 31m } });

        var result = await _service.ConsultarCustodiaMasterAsync();

        result.Should().NotBeNull();
        result.ContaMaster.NumeroConta.Should().Be("MST-000001");
        result.Custodia.Should().HaveCount(2);
        result.ValorTotalResiduo.Should().BeGreaterThan(0);
    }
}
