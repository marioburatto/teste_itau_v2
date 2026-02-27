using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Interfaces;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;

namespace CompraProgramada.Application.Services;

public class CestaService
{
    private readonly ICestaRepository _cestaRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly ICotahistService _cotahistService;
    private readonly string _pastaCotacoes;

    public CestaService(
        ICestaRepository cestaRepository,
        IClienteRepository clienteRepository,
        ICotahistService cotahistService,
        string pastaCotacoes = "cotacoes")
    {
        _cestaRepository = cestaRepository;
        _clienteRepository = clienteRepository;
        _cotahistService = cotahistService;
        _pastaCotacoes = pastaCotacoes;
    }

    public async Task<CestaResponse> CadastrarOuAlterarAsync(CestaRequest request)
    {
        if (request.Itens.Count != 5)
            throw new BusinessException(
                $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {request.Itens.Count}.",
                "QUANTIDADE_ATIVOS_INVALIDA");

        var somaPercentuais = request.Itens.Sum(i => i.Percentual);
        if (Math.Abs(somaPercentuais - 100m) > 0.01m)
            throw new BusinessException(
                $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {somaPercentuais}%.",
                "PERCENTUAIS_INVALIDOS");

        foreach (var item in request.Itens)
        {
            if (item.Percentual <= 0)
                throw new BusinessException(
                    $"Cada percentual deve ser maior que 0%. Ticker: {item.Ticker}.",
                    "PERCENTUAIS_INVALIDOS");
        }

        var cestaAnterior = await _cestaRepository.GetAtivaAsync();

        var novaCesta = new CestaRecomendacao
        {
            Nome = request.Nome,
            Ativa = true,
            DataCriacao = DateTime.UtcNow,
            Itens = request.Itens.Select(i => new CestaItem
            {
                Ticker = i.Ticker.ToUpper().Trim(),
                Percentual = i.Percentual
            }).ToList()
        };

        novaCesta = await _cestaRepository.AddAsync(novaCesta);

        var response = new CestaResponse
        {
            CestaId = novaCesta.Id,
            Nome = novaCesta.Nome,
            Ativa = true,
            DataCriacao = novaCesta.DataCriacao,
            Itens = novaCesta.Itens.Select(i => new CestaItemResponse
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual
            }).ToList()
        };

        if (cestaAnterior != null)
        {
            cestaAnterior.Ativa = false;
            cestaAnterior.DataDesativacao = DateTime.UtcNow;
            await _cestaRepository.UpdateAsync(cestaAnterior);

            var tickersAnteriores = cestaAnterior.Itens.Select(i => i.Ticker).ToHashSet();
            var tickersNovos = novaCesta.Itens.Select(i => i.Ticker).ToHashSet();

            var removidos = tickersAnteriores.Except(tickersNovos).ToList();
            var adicionados = tickersNovos.Except(tickersAnteriores).ToList();

            var clientesAtivos = await _clienteRepository.GetAtivosAsync();
            bool temRebalanceamento = removidos.Any() || adicionados.Any() ||
                cestaAnterior.Itens.Any(a =>
                {
                    var novoItem = novaCesta.Itens.FirstOrDefault(n => n.Ticker == a.Ticker);
                    return novoItem != null && novoItem.Percentual != a.Percentual;
                });

            response.CestaAnteriorDesativada = new CestaDesativadaDto
            {
                CestaId = cestaAnterior.Id,
                Nome = cestaAnterior.Nome,
                DataDesativacao = cestaAnterior.DataDesativacao
            };
            response.RebalanceamentoDisparado = temRebalanceamento;
            response.AtivosRemovidos = removidos;
            response.AtivosAdicionados = adicionados;
            response.Mensagem = temRebalanceamento
                ? $"Cesta atualizada. Rebalanceamento disparado para {clientesAtivos.Count} clientes ativos."
                : "Cesta atualizada com sucesso.";
        }
        else
        {
            response.RebalanceamentoDisparado = false;
            response.Mensagem = "Primeira cesta cadastrada com sucesso.";
        }

        return response;
    }

    public async Task<CestaAtualResponse> ObterAtualAsync()
    {
        var cesta = await _cestaRepository.GetAtivaAsync();
        if (cesta == null)
            throw new NotFoundException("Nenhuma cesta ativa encontrada.", "CESTA_NAO_ENCONTRADA");

        var tickers = cesta.Itens.Select(i => i.Ticker).ToList();
        var cotacoes = _cotahistService.ObterCotacoesFechamento(_pastaCotacoes, tickers);

        return new CestaAtualResponse
        {
            CestaId = cesta.Id,
            Nome = cesta.Nome,
            Ativa = cesta.Ativa,
            DataCriacao = cesta.DataCriacao,
            Itens = cesta.Itens.Select(i => new CestaItemResponse
            {
                Ticker = i.Ticker,
                Percentual = i.Percentual,
                CotacaoAtual = cotacoes.ContainsKey(i.Ticker) ? cotacoes[i.Ticker] : null
            }).ToList()
        };
    }

    public async Task<HistoricoCestasResponse> ObterHistoricoAsync()
    {
        var cestas = await _cestaRepository.GetHistoricoAsync();

        return new HistoricoCestasResponse
        {
            Cestas = cestas.OrderByDescending(c => c.DataCriacao).Select(c => new CestaHistoricoDto
            {
                CestaId = c.Id,
                Nome = c.Nome,
                Ativa = c.Ativa,
                DataCriacao = c.DataCriacao,
                DataDesativacao = c.DataDesativacao,
                Itens = c.Itens.Select(i => new CestaItemResponse
                {
                    Ticker = i.Ticker,
                    Percentual = i.Percentual
                }).ToList()
            }).ToList()
        };
    }
}
