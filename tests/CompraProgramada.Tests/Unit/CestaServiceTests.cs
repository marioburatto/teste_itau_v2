using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Interfaces;
using CompraProgramada.Application.Services;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;

namespace CompraProgramada.Tests.Unit;

public class CestaServiceTests
{
    private readonly Mock<ICestaRepository> _cestaRepoMock;
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICotahistService> _cotahistMock;
    private readonly CestaService _service;

    public CestaServiceTests()
    {
        _cestaRepoMock = new Mock<ICestaRepository>();
        _clienteRepoMock = new Mock<IClienteRepository>();
        _cotahistMock = new Mock<ICotahistService>();
        _service = new CestaService(
            _cestaRepoMock.Object,
            _clienteRepoMock.Object,
            _cotahistMock.Object,
            "cotacoes_test");
    }

    [Fact]
    public async Task CadastrarOuAlterar_DeveCriarPrimeiraCesta_QuandoNenhumaCestaExiste()
    {
        var request = new CestaRequest
        {
            Nome = "Top Five - Fevereiro 2026",
            Itens = new List<CestaItemRequest>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 10m }
            }
        };

        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync((CestaRecomendacao?)null);
        _cestaRepoMock.Setup(r => r.AddAsync(It.IsAny<CestaRecomendacao>()))
            .ReturnsAsync((CestaRecomendacao c) => { c.Id = 1; return c; });

        var result = await _service.CadastrarOuAlterarAsync(request);

        result.Should().NotBeNull();
        result.CestaId.Should().Be(1);
        result.Nome.Should().Be("Top Five - Fevereiro 2026");
        result.Ativa.Should().BeTrue();
        result.Itens.Should().HaveCount(5);
        result.RebalanceamentoDisparado.Should().BeFalse();
        result.Mensagem.Should().Contain("Primeira cesta");
    }

    [Fact]
    public async Task CadastrarOuAlterar_DeveDesativarCestaAnterior_EDispararRebalanceamento()
    {
        var cestaAnterior = new CestaRecomendacao
        {
            Id = 1,
            Nome = "Top Five - Fevereiro 2026",
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

        var request = new CestaRequest
        {
            Nome = "Top Five - Marco 2026",
            Itens = new List<CestaItemRequest>
            {
                new() { Ticker = "PETR4", Percentual = 25m },
                new() { Ticker = "VALE3", Percentual = 20m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "ABEV3", Percentual = 20m },
                new() { Ticker = "RENT3", Percentual = 15m }
            }
        };

        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(cestaAnterior);
        _cestaRepoMock.Setup(r => r.AddAsync(It.IsAny<CestaRecomendacao>()))
            .ReturnsAsync((CestaRecomendacao c) => { c.Id = 2; return c; });
        _cestaRepoMock.Setup(r => r.UpdateAsync(It.IsAny<CestaRecomendacao>())).Returns(Task.CompletedTask);
        _clienteRepoMock.Setup(r => r.GetAtivosAsync()).ReturnsAsync(new List<Cliente>
        {
            new() { Id = 1, Nome = "Joao", Ativo = true },
            new() { Id = 2, Nome = "Maria", Ativo = true }
        });

        var result = await _service.CadastrarOuAlterarAsync(request);

        result.Should().NotBeNull();
        result.CestaId.Should().Be(2);
        result.RebalanceamentoDisparado.Should().BeTrue();
        result.AtivosRemovidos.Should().Contain(new[] { "BBDC4", "WEGE3" });
        result.AtivosAdicionados.Should().Contain(new[] { "ABEV3", "RENT3" });
        result.CestaAnteriorDesativada.Should().NotBeNull();
        result.CestaAnteriorDesativada!.CestaId.Should().Be(1);
    }

    [Fact]
    public async Task CadastrarOuAlterar_DeveLancarExcecao_QuandoNaoTem5Ativos()
    {
        var request = new CestaRequest
        {
            Nome = "Top Five",
            Itens = new List<CestaItemRequest>
            {
                new() { Ticker = "PETR4", Percentual = 50m },
                new() { Ticker = "VALE3", Percentual = 50m }
            }
        };

        var act = () => _service.CadastrarOuAlterarAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("QUANTIDADE_ATIVOS_INVALIDA");
    }

    [Fact]
    public async Task CadastrarOuAlterar_DeveLancarExcecao_QuandoPercentuaisNaoSomam100()
    {
        var request = new CestaRequest
        {
            Nome = "Top Five",
            Itens = new List<CestaItemRequest>
            {
                new() { Ticker = "PETR4", Percentual = 30m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 5m }
            }
        };

        var act = () => _service.CadastrarOuAlterarAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("PERCENTUAIS_INVALIDOS");
    }

    [Fact]
    public async Task CadastrarOuAlterar_DeveLancarExcecao_QuandoPercentualZero()
    {
        var request = new CestaRequest
        {
            Nome = "Top Five",
            Itens = new List<CestaItemRequest>
            {
                new() { Ticker = "PETR4", Percentual = 40m },
                new() { Ticker = "VALE3", Percentual = 25m },
                new() { Ticker = "ITUB4", Percentual = 20m },
                new() { Ticker = "BBDC4", Percentual = 15m },
                new() { Ticker = "WEGE3", Percentual = 0m }
            }
        };

        var act = () => _service.CadastrarOuAlterarAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("PERCENTUAIS_INVALIDOS");
    }

    [Fact]
    public async Task ObterAtual_DeveRetornarCesta_ComCotacoes()
    {
        var cesta = new CestaRecomendacao
        {
            Id = 1,
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

        var cotacoes = new Dictionary<string, decimal>
        {
            { "PETR4", 35m }, { "VALE3", 62m }, { "ITUB4", 30m },
            { "BBDC4", 15m }, { "WEGE3", 40m }
        };

        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync(cesta);
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(cotacoes);

        var result = await _service.ObterAtualAsync();

        result.Should().NotBeNull();
        result.CestaId.Should().Be(1);
        result.Itens.Should().HaveCount(5);
        result.Itens.First(i => i.Ticker == "PETR4").CotacaoAtual.Should().Be(35m);
    }

    [Fact]
    public async Task ObterAtual_DeveLancarExcecao_QuandoNenhumaCestaAtiva()
    {
        _cestaRepoMock.Setup(r => r.GetAtivaAsync()).ReturnsAsync((CestaRecomendacao?)null);

        var act = () => _service.ObterAtualAsync();

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be("CESTA_NAO_ENCONTRADA");
    }

    [Fact]
    public async Task ObterHistorico_DeveRetornarTodasCestas()
    {
        var cestas = new List<CestaRecomendacao>
        {
            new() { Id = 2, Nome = "Cesta 2", Ativa = true, DataCriacao = DateTime.UtcNow, Itens = new List<CestaItem>() },
            new() { Id = 1, Nome = "Cesta 1", Ativa = false, DataCriacao = DateTime.UtcNow.AddDays(-30), Itens = new List<CestaItem>() }
        };

        _cestaRepoMock.Setup(r => r.GetHistoricoAsync()).ReturnsAsync(cestas);

        var result = await _service.ObterHistoricoAsync();

        result.Should().NotBeNull();
        result.Cestas.Should().HaveCount(2);
    }
}
