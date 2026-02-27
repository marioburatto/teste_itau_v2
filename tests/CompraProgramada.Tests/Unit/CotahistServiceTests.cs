using CompraProgramada.Infrastructure.Cotacoes;
using FluentAssertions;
using Xunit;

namespace CompraProgramada.Tests.Unit;

public class CotahistServiceTests
{
    private readonly CotahistService _service;
    private readonly string _testCotacoesPath;

    public CotahistServiceTests()
    {
        _service = new CotahistService();
        _testCotacoesPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "cotacoes_test");
    }

    [Fact]
    public void ParseArquivo_DeveRetornarCotacoes_QuandoArquivoValido()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        cotacoes.Should().NotBeEmpty();
        cotacoes.Should().HaveCountGreaterThan(0);
    }

    [Fact]
    public void ParseArquivo_DeveFiltrarApenasRegistrosDetalhe()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        // Should not include header (00) or trailer (99)
        cotacoes.Should().OnlyContain(c => c.Ticker != null && c.Ticker.Length > 0);
    }

    [Fact]
    public void ParseArquivo_DeveConterPETR4_ComPrecoFechamentoCorreto()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var petr4 = cotacoes.FirstOrDefault(c => c.Ticker == "PETR4" && c.TipoMercado == 10);
        petr4.Should().NotBeNull();
        petr4!.PrecoFechamento.Should().Be(35.00m);
        petr4.CodigoBDI.Should().Be("02");
    }

    [Fact]
    public void ParseArquivo_DeveConterVALE3_ComPrecoFechamentoCorreto()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var vale3 = cotacoes.FirstOrDefault(c => c.Ticker == "VALE3" && c.TipoMercado == 10);
        vale3.Should().NotBeNull();
        vale3!.PrecoFechamento.Should().Be(62.00m);
    }

    [Fact]
    public void ParseArquivo_DeveConterITUB4_ComPrecoFechamentoCorreto()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var itub4 = cotacoes.FirstOrDefault(c => c.Ticker == "ITUB4" && c.TipoMercado == 10);
        itub4.Should().NotBeNull();
        itub4!.PrecoFechamento.Should().Be(30.00m);
    }

    [Fact]
    public void ParseArquivo_DeveConterBBDC4_ComPrecoFechamentoCorreto()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var bbdc4 = cotacoes.FirstOrDefault(c => c.Ticker == "BBDC4" && c.TipoMercado == 10);
        bbdc4.Should().NotBeNull();
        bbdc4!.PrecoFechamento.Should().Be(15.00m);
    }

    [Fact]
    public void ParseArquivo_DeveConterWEGE3_ComPrecoFechamentoCorreto()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var wege3 = cotacoes.FirstOrDefault(c => c.Ticker == "WEGE3" && c.TipoMercado == 10);
        wege3.Should().NotBeNull();
        wege3!.PrecoFechamento.Should().Be(40.00m);
    }

    [Fact]
    public void ParseArquivo_DeveSepararMercadoVistaEFracionario()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var vistaCount = cotacoes.Count(c => c.TipoMercado == 10);
        var fracCount = cotacoes.Count(c => c.TipoMercado == 20);

        vistaCount.Should().BeGreaterThan(0);
        fracCount.Should().BeGreaterThan(0);
    }

    [Fact]
    public void ParseArquivo_FracionarioDeveTerSufixoF()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var fracionarios = cotacoes.Where(c => c.TipoMercado == 20).ToList();
        fracionarios.Should().OnlyContain(c => c.Ticker.EndsWith("F"));
    }

    [Fact]
    public void ParseArquivo_DeveParseDataPregaoCorretamente()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var primeiro = cotacoes.First();
        primeiro.DataPregao.Should().Be(new DateTime(2026, 2, 25));
    }

    [Fact]
    public void ObterCotacaoFechamento_DeveRetornarCotacao_QuandoTickerExiste()
    {
        var cotacao = _service.ObterCotacaoFechamento(_testCotacoesPath, "PETR4");

        cotacao.Should().NotBeNull();
        cotacao!.Ticker.Should().Be("PETR4");
        cotacao.PrecoFechamento.Should().Be(35.00m);
    }

    [Fact]
    public void ObterCotacaoFechamento_DeveRetornarNull_QuandoTickerNaoExiste()
    {
        var cotacao = _service.ObterCotacaoFechamento(_testCotacoesPath, "XYZW3");

        cotacao.Should().BeNull();
    }

    [Fact]
    public void ObterCotacaoFechamento_DeveRetornarNull_QuandoPastaNaoExiste()
    {
        var cotacao = _service.ObterCotacaoFechamento("/pasta/inexistente", "PETR4");

        cotacao.Should().BeNull();
    }

    [Fact]
    public void ObterCotacoesFechamento_DeveRetornarMultiplasCotacoes()
    {
        var tickers = new[] { "PETR4", "VALE3", "ITUB4", "BBDC4", "WEGE3" };
        var cotacoes = _service.ObterCotacoesFechamento(_testCotacoesPath, tickers);

        cotacoes.Should().HaveCount(5);
        cotacoes["PETR4"].Should().Be(35.00m);
        cotacoes["VALE3"].Should().Be(62.00m);
        cotacoes["ITUB4"].Should().Be(30.00m);
        cotacoes["BBDC4"].Should().Be(15.00m);
        cotacoes["WEGE3"].Should().Be(40.00m);
    }

    [Fact]
    public void ObterCotacoesFechamento_DeveRetornarVazio_QuandoPastaNaoExiste()
    {
        var tickers = new[] { "PETR4" };
        var cotacoes = _service.ObterCotacoesFechamento("/pasta/inexistente", tickers);

        cotacoes.Should().BeEmpty();
    }

    [Fact]
    public void ParseArquivo_DeveConterABEV3_ComPrecoFechamentoCorreto()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var abev3 = cotacoes.FirstOrDefault(c => c.Ticker == "ABEV3" && c.TipoMercado == 10);
        abev3.Should().NotBeNull();
        abev3!.PrecoFechamento.Should().Be(14.00m);
    }

    [Fact]
    public void ParseArquivo_DeveConterRENT3_ComPrecoFechamentoCorreto()
    {
        var arquivo = Path.Combine(_testCotacoesPath, "COTAHIST_D20260225.TXT");
        var cotacoes = _service.ParseArquivo(arquivo).ToList();

        var rent3 = cotacoes.FirstOrDefault(c => c.Ticker == "RENT3" && c.TipoMercado == 10);
        rent3.Should().NotBeNull();
        rent3!.PrecoFechamento.Should().Be(48.00m);
    }
}
