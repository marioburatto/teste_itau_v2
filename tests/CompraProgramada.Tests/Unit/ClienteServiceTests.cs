using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Services;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;
using FluentAssertions;
using Moq;
using Xunit;
using CompraProgramada.Application.Interfaces;

namespace CompraProgramada.Tests.Unit;

public class ClienteServiceTests
{
    private readonly Mock<IClienteRepository> _clienteRepoMock;
    private readonly Mock<ICustodiaFilhoteRepository> _custodiaRepoMock;
    private readonly Mock<ICotahistService> _cotahistMock;
    private readonly ClienteService _service;

    public ClienteServiceTests()
    {
        _clienteRepoMock = new Mock<IClienteRepository>();
        _custodiaRepoMock = new Mock<ICustodiaFilhoteRepository>();
        _cotahistMock = new Mock<ICotahistService>();
        _service = new ClienteService(
            _clienteRepoMock.Object,
            _custodiaRepoMock.Object,
            _cotahistMock.Object,
            "cotacoes_test");
    }

    [Fact]
    public async Task AderirAsync_DeveRetornarAdesao_QuandoDadosValidos()
    {
        var request = new AdesaoRequest
        {
            Nome = "Joao da Silva",
            Cpf = "12345678901",
            Email = "joao@email.com",
            ValorMensal = 3000m
        };

        _clienteRepoMock.Setup(r => r.GetByCpfAsync(It.IsAny<string>())).ReturnsAsync((Cliente?)null);
        _clienteRepoMock.Setup(r => r.AddAsync(It.IsAny<Cliente>()))
            .ReturnsAsync((Cliente c) => { c.Id = 1; return c; });
        _clienteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        var result = await _service.AderirAsync(request);

        result.Should().NotBeNull();
        result.ClienteId.Should().Be(1);
        result.Nome.Should().Be("Joao da Silva");
        result.Cpf.Should().Be("12345678901");
        result.ValorMensal.Should().Be(3000m);
        result.Ativo.Should().BeTrue();
        result.ContaGrafica.Should().NotBeNull();
        result.ContaGrafica!.NumeroConta.Should().Be("FLH-000001");
        result.ContaGrafica.Tipo.Should().Be("FILHOTE");
    }

    [Fact]
    public async Task AderirAsync_DeveLancarExcecao_QuandoCpfDuplicado()
    {
        var request = new AdesaoRequest
        {
            Nome = "Joao da Silva",
            Cpf = "12345678901",
            Email = "joao@email.com",
            ValorMensal = 3000m
        };

        _clienteRepoMock.Setup(r => r.GetByCpfAsync("12345678901"))
            .ReturnsAsync(new Cliente { Id = 1, Cpf = "12345678901" });

        var act = () => _service.AderirAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("CLIENTE_CPF_DUPLICADO");
    }

    [Fact]
    public async Task AderirAsync_DeveLancarExcecao_QuandoValorMensalAbaixoMinimo()
    {
        var request = new AdesaoRequest
        {
            Nome = "Joao da Silva",
            Cpf = "12345678901",
            Email = "joao@email.com",
            ValorMensal = 50m
        };

        var act = () => _service.AderirAsync(request);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("VALOR_MENSAL_INVALIDO");
    }

    [Fact]
    public async Task AderirAsync_DeveLancarExcecao_QuandoNomeVazio()
    {
        var request = new AdesaoRequest
        {
            Nome = "",
            Cpf = "12345678901",
            Email = "joao@email.com",
            ValorMensal = 3000m
        };

        var act = () => _service.AderirAsync(request);
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task AderirAsync_DeveLancarExcecao_QuandoCpfVazio()
    {
        var request = new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "",
            Email = "joao@email.com",
            ValorMensal = 3000m
        };

        var act = () => _service.AderirAsync(request);
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task AderirAsync_DeveLancarExcecao_QuandoEmailVazio()
    {
        var request = new AdesaoRequest
        {
            Nome = "Joao",
            Cpf = "12345678901",
            Email = "",
            ValorMensal = 3000m
        };

        var act = () => _service.AderirAsync(request);
        await act.Should().ThrowAsync<BusinessException>();
    }

    [Fact]
    public async Task SairAsync_DeveDesativarCliente_QuandoClienteAtivo()
    {
        var cliente = new Cliente
        {
            Id = 1,
            Nome = "Joao da Silva",
            Ativo = true
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);
        _clienteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        var result = await _service.SairAsync(1);

        result.Should().NotBeNull();
        result.ClienteId.Should().Be(1);
        result.Ativo.Should().BeFalse();
        result.DataSaida.Should().NotBeNull();
        result.Mensagem.Should().Contain("custodia foi mantida");
    }

    [Fact]
    public async Task SairAsync_DeveLancarExcecao_QuandoClienteNaoEncontrado()
    {
        _clienteRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Cliente?)null);

        var act = () => _service.SairAsync(999);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be("CLIENTE_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task SairAsync_DeveLancarExcecao_QuandoClienteJaInativo()
    {
        var cliente = new Cliente { Id = 1, Nome = "Joao", Ativo = false };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var act = () => _service.SairAsync(1);

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("CLIENTE_JA_INATIVO");
    }

    [Fact]
    public async Task AlterarValorMensalAsync_DeveAtualizar_QuandoValorValido()
    {
        var cliente = new Cliente
        {
            Id = 1,
            Nome = "Joao",
            ValorMensal = 3000m,
            HistoricoValoresMensais = new List<HistoricoValorMensal>()
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);
        _clienteRepoMock.Setup(r => r.UpdateAsync(It.IsAny<Cliente>())).Returns(Task.CompletedTask);

        var result = await _service.AlterarValorMensalAsync(1, new AlterarValorMensalRequest { NovoValorMensal = 6000m });

        result.Should().NotBeNull();
        result.ValorMensalAnterior.Should().Be(3000m);
        result.ValorMensalNovo.Should().Be(6000m);
        result.Mensagem.Should().Contain("atualizado");
    }

    [Fact]
    public async Task AlterarValorMensalAsync_DeveLancarExcecao_QuandoValorAbaixoMinimo()
    {
        var cliente = new Cliente { Id = 1, Nome = "Joao", ValorMensal = 3000m };
        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);

        var act = () => _service.AlterarValorMensalAsync(1, new AlterarValorMensalRequest { NovoValorMensal = 50m });

        var ex = await act.Should().ThrowAsync<BusinessException>();
        ex.Which.Codigo.Should().Be("VALOR_MENSAL_INVALIDO");
    }

    [Fact]
    public async Task AlterarValorMensalAsync_DeveLancarExcecao_QuandoClienteNaoEncontrado()
    {
        _clienteRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Cliente?)null);

        var act = () => _service.AlterarValorMensalAsync(999, new AlterarValorMensalRequest { NovoValorMensal = 6000m });

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be("CLIENTE_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task ConsultarCarteiraAsync_DeveRetornarCarteira_ComCalculosCorretos()
    {
        var cliente = new Cliente
        {
            Id = 1,
            Nome = "Joao",
            ContaGrafica = new ContaGrafica { NumeroConta = "FLH-000001" }
        };

        var custodias = new List<CustodiaFilhote>
        {
            new() { ClienteId = 1, Ticker = "PETR4", Quantidade = 10, PrecoMedio = 30m },
            new() { ClienteId = 1, Ticker = "VALE3", Quantidade = 5, PrecoMedio = 55m }
        };

        var cotacoes = new Dictionary<string, decimal>
        {
            { "PETR4", 35m },
            { "VALE3", 62m }
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);
        _custodiaRepoMock.Setup(r => r.GetByClienteIdAsync(1)).ReturnsAsync(custodias);
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(cotacoes);

        var result = await _service.ConsultarCarteiraAsync(1);

        result.Should().NotBeNull();
        result.ClienteId.Should().Be(1);
        result.ContaGrafica.Should().Be("FLH-000001");

        // PETR4: 10 * 35 = 350, VALE3: 5 * 62 = 310 => Total = 660
        result.Resumo.ValorAtualCarteira.Should().Be(660m);

        // Invested: PETR4: 10 * 30 = 300, VALE3: 5 * 55 = 275 => Total = 575
        result.Resumo.ValorTotalInvestido.Should().Be(575m);

        // PL: 660 - 575 = 85
        result.Resumo.PlTotal.Should().Be(85m);

        result.Ativos.Should().HaveCount(2);

        var petr4 = result.Ativos.First(a => a.Ticker == "PETR4");
        petr4.Quantidade.Should().Be(10);
        petr4.PrecoMedio.Should().Be(30m);
        petr4.CotacaoAtual.Should().Be(35m);
        petr4.ValorAtual.Should().Be(350m);
        petr4.Pl.Should().Be(50m); // (35-30)*10
    }

    [Fact]
    public async Task ConsultarCarteiraAsync_DeveLancarExcecao_QuandoClienteNaoEncontrado()
    {
        _clienteRepoMock.Setup(r => r.GetByIdAsync(999)).ReturnsAsync((Cliente?)null);

        var act = () => _service.ConsultarCarteiraAsync(999);

        var ex = await act.Should().ThrowAsync<NotFoundException>();
        ex.Which.Codigo.Should().Be("CLIENTE_NAO_ENCONTRADO");
    }

    [Fact]
    public async Task ConsultarRentabilidadeAsync_DeveRetornarDados()
    {
        var cliente = new Cliente
        {
            Id = 1,
            Nome = "Joao",
            ContaGrafica = new ContaGrafica { NumeroConta = "FLH-000001" },
            HistoricoAportes = new List<HistoricoAporte>
            {
                new() { Data = new DateTime(2026, 1, 5), Valor = 1000m, Parcela = "1/3" },
                new() { Data = new DateTime(2026, 1, 15), Valor = 1000m, Parcela = "2/3" }
            }
        };

        _clienteRepoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(cliente);
        _custodiaRepoMock.Setup(r => r.GetByClienteIdAsync(1)).ReturnsAsync(new List<CustodiaFilhote>());
        _cotahistMock.Setup(s => s.ObterCotacoesFechamento(It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new Dictionary<string, decimal>());

        var result = await _service.ConsultarRentabilidadeAsync(1);

        result.Should().NotBeNull();
        result.ClienteId.Should().Be(1);
        result.HistoricoAportes.Should().HaveCount(2);
    }
}
