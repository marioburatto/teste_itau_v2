using CompraProgramada.Application.Interfaces;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Interfaces;

namespace CompraProgramada.Application.Services;

public class RebalanceamentoService
{
    private readonly IClienteRepository _clienteRepository;
    private readonly ICestaRepository _cestaRepository;
    private readonly ICustodiaFilhoteRepository _custodiaFilhoteRepository;
    private readonly IVendaRebalanceamentoRepository _vendaRepository;
    private readonly ICotahistService _cotahistService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly string _pastaCotacoes;

    private const decimal LIMIAR_DESVIO = 5.0m; // 5 percentage points

    public RebalanceamentoService(
        IClienteRepository clienteRepository,
        ICestaRepository cestaRepository,
        ICustodiaFilhoteRepository custodiaFilhoteRepository,
        IVendaRebalanceamentoRepository vendaRepository,
        ICotahistService cotahistService,
        IKafkaProducer kafkaProducer,
        string pastaCotacoes = "cotacoes")
    {
        _clienteRepository = clienteRepository;
        _cestaRepository = cestaRepository;
        _custodiaFilhoteRepository = custodiaFilhoteRepository;
        _vendaRepository = vendaRepository;
        _cotahistService = cotahistService;
        _kafkaProducer = kafkaProducer;
        _pastaCotacoes = pastaCotacoes;
    }

    public async Task RebalancearPorMudancaCestaAsync(CestaRecomendacao cestaAnterior, CestaRecomendacao novaCesta)
    {
        var clientesAtivos = await _clienteRepository.GetAtivosAsync();
        if (clientesAtivos.Count == 0) return;

        var tickersAnteriores = cestaAnterior.Itens.Select(i => i.Ticker).ToHashSet();
        var tickersNovos = novaCesta.Itens.Select(i => i.Ticker).ToHashSet();
        var todosTickersRelevantes = tickersAnteriores.Union(tickersNovos).ToList();

        var cotacoes = _cotahistService.ObterCotacoesFechamento(_pastaCotacoes, todosTickersRelevantes);

        foreach (var cliente in clientesAtivos)
        {
            await RebalancearClientePorMudancaAsync(cliente, cestaAnterior, novaCesta, cotacoes);
        }
    }

    private async Task RebalancearClientePorMudancaAsync(
        Cliente cliente,
        CestaRecomendacao cestaAnterior,
        CestaRecomendacao novaCesta,
        Dictionary<string, decimal> cotacoes)
    {
        var custodias = await _custodiaFilhoteRepository.GetByClienteIdAsync(cliente.Id);
        if (custodias.Count == 0) return;

        var tickersAnteriores = cestaAnterior.Itens.Select(i => i.Ticker).ToHashSet();
        var tickersNovos = novaCesta.Itens.Select(i => i.Ticker).ToHashSet();
        var removidos = tickersAnteriores.Except(tickersNovos).ToList();
        var adicionados = tickersNovos.Except(tickersAnteriores).ToList();

        // Step 1: Sell removed assets
        decimal valorObtidoVendas = 0;
        var vendasMes = new List<VendaRebalanceamento>();

        foreach (var ticker in removidos)
        {
            var custodia = custodias.FirstOrDefault(c => c.Ticker == ticker);
            if (custodia == null || custodia.Quantidade <= 0) continue;

            var preco = cotacoes.ContainsKey(ticker) ? cotacoes[ticker] : custodia.PrecoMedio;
            var valorVenda = custodia.Quantidade * preco;
            var lucro = (preco - custodia.PrecoMedio) * custodia.Quantidade;

            valorObtidoVendas += valorVenda;

            var venda = new VendaRebalanceamento
            {
                ClienteId = cliente.Id,
                Ticker = ticker,
                Quantidade = custodia.Quantidade,
                PrecoVenda = preco,
                PrecoMedio = custodia.PrecoMedio,
                ValorVenda = valorVenda,
                Lucro = lucro,
                DataVenda = DateTime.UtcNow
            };
            await _vendaRepository.AddAsync(venda);
            vendasMes.Add(venda);

            // Remove from custody
            custodia.Quantidade = 0;
            await _custodiaFilhoteRepository.UpdateAsync(custodia);
        }

        // Step 2: Rebalance assets that changed percentage
        var valorTotalCarteira = custodias
            .Where(c => c.Quantidade > 0)
            .Sum(c => c.Quantidade * (cotacoes.ContainsKey(c.Ticker) ? cotacoes[c.Ticker] : c.PrecoMedio));
        valorTotalCarteira += valorObtidoVendas;

        foreach (var itemNovo in novaCesta.Itens)
        {
            if (adicionados.Contains(itemNovo.Ticker)) continue; // handled below

            var custodia = custodias.FirstOrDefault(c => c.Ticker == itemNovo.Ticker);
            if (custodia == null) continue;

            var cotacao = cotacoes.ContainsKey(itemNovo.Ticker) ? cotacoes[itemNovo.Ticker] : custodia.PrecoMedio;
            var valorAtual = custodia.Quantidade * cotacao;
            var valorAlvo = valorTotalCarteira * (itemNovo.Percentual / 100m);

            if (valorAtual > valorAlvo)
            {
                // Sell excess
                var excesso = (int)Math.Truncate((valorAtual - valorAlvo) / cotacao);
                if (excesso > 0 && excesso <= custodia.Quantidade)
                {
                    var valorVenda = excesso * cotacao;
                    var lucro = (cotacao - custodia.PrecoMedio) * excesso;
                    valorObtidoVendas += valorVenda;

                    var venda = new VendaRebalanceamento
                    {
                        ClienteId = cliente.Id,
                        Ticker = itemNovo.Ticker,
                        Quantidade = excesso,
                        PrecoVenda = cotacao,
                        PrecoMedio = custodia.PrecoMedio,
                        ValorVenda = valorVenda,
                        Lucro = lucro,
                        DataVenda = DateTime.UtcNow
                    };
                    await _vendaRepository.AddAsync(venda);
                    vendasMes.Add(venda);

                    custodia.Quantidade -= excesso;
                    await _custodiaFilhoteRepository.UpdateAsync(custodia);
                }
            }
        }

        // Step 3: Buy new assets proportionally with proceeds
        if (valorObtidoVendas > 0 && adicionados.Count > 0)
        {
            var somaPercentuaisNovos = novaCesta.Itens
                .Where(i => adicionados.Contains(i.Ticker))
                .Sum(i => i.Percentual);

            foreach (var ticker in adicionados)
            {
                var itemNovo = novaCesta.Itens.First(i => i.Ticker == ticker);
                var proporcao = somaPercentuaisNovos > 0 ? itemNovo.Percentual / somaPercentuaisNovos : 0;
                var valorParaComprar = valorObtidoVendas * proporcao;

                if (!cotacoes.ContainsKey(ticker) || cotacoes[ticker] <= 0) continue;
                var cotacao = cotacoes[ticker];
                var quantidade = (int)Math.Truncate(valorParaComprar / cotacao);

                if (quantidade > 0)
                {
                    var custodia = await _custodiaFilhoteRepository.GetByClienteAndTickerAsync(cliente.Id, ticker);
                    if (custodia == null)
                    {
                        custodia = new CustodiaFilhote
                        {
                            ClienteId = cliente.Id,
                            Ticker = ticker,
                            Quantidade = quantidade,
                            PrecoMedio = cotacao,
                            ValorTotalInvestido = quantidade * cotacao,
                            DataAtualizacao = DateTime.UtcNow
                        };
                        await _custodiaFilhoteRepository.AddAsync(custodia);
                    }
                    else
                    {
                        var novoPrecoMedio = ((custodia.Quantidade * custodia.PrecoMedio) + (quantidade * cotacao))
                            / (custodia.Quantidade + quantidade);
                        custodia.Quantidade += quantidade;
                        custodia.PrecoMedio = novoPrecoMedio;
                        custodia.ValorTotalInvestido += quantidade * cotacao;
                        custodia.DataAtualizacao = DateTime.UtcNow;
                        await _custodiaFilhoteRepository.UpdateAsync(custodia);
                    }
                }
            }

            // Also buy under-allocated existing assets
            foreach (var itemNovo in novaCesta.Itens)
            {
                if (adicionados.Contains(itemNovo.Ticker)) continue;

                var custodia = custodias.FirstOrDefault(c => c.Ticker == itemNovo.Ticker);
                if (custodia == null) continue;

                var cotacao = cotacoes.ContainsKey(itemNovo.Ticker) ? cotacoes[itemNovo.Ticker] : custodia.PrecoMedio;
                var valorAtual = custodia.Quantidade * cotacao;
                var valorAlvo = valorTotalCarteira * (itemNovo.Percentual / 100m);

                if (valorAtual < valorAlvo)
                {
                    var deficit = (int)Math.Truncate((valorAlvo - valorAtual) / cotacao);
                    if (deficit > 0)
                    {
                        var novoPrecoMedio = ((custodia.Quantidade * custodia.PrecoMedio) + (deficit * cotacao))
                            / (custodia.Quantidade + deficit);
                        custodia.Quantidade += deficit;
                        custodia.PrecoMedio = novoPrecoMedio;
                        custodia.ValorTotalInvestido += deficit * cotacao;
                        custodia.DataAtualizacao = DateTime.UtcNow;
                        await _custodiaFilhoteRepository.UpdateAsync(custodia);
                    }
                }
            }
        }

        // Step 4: Calculate IR on sales
        await CalcularIRVendasAsync(cliente, vendasMes);
    }

    public async Task RebalancearPorDesvioProporcaoAsync()
    {
        var cesta = await _cestaRepository.GetAtivaAsync();
        if (cesta == null) return;

        var clientesAtivos = await _clienteRepository.GetAtivosAsync();
        var tickers = cesta.Itens.Select(i => i.Ticker).ToList();
        var cotacoes = _cotahistService.ObterCotacoesFechamento(_pastaCotacoes, tickers);

        foreach (var cliente in clientesAtivos)
        {
            var custodias = await _custodiaFilhoteRepository.GetByClienteIdAsync(cliente.Id);
            if (custodias.Count == 0) continue;

            var valorTotal = custodias.Sum(c =>
                c.Quantidade * (cotacoes.ContainsKey(c.Ticker) ? cotacoes[c.Ticker] : c.PrecoMedio));

            if (valorTotal <= 0) continue;

            bool temDesvio = false;
            foreach (var item in cesta.Itens)
            {
                var custodia = custodias.FirstOrDefault(c => c.Ticker == item.Ticker);
                var valorAtivo = custodia != null
                    ? custodia.Quantidade * (cotacoes.ContainsKey(item.Ticker) ? cotacoes[item.Ticker] : custodia.PrecoMedio)
                    : 0m;
                var proporcaoAtual = valorTotal > 0 ? (valorAtivo / valorTotal) * 100m : 0m;
                var desvio = Math.Abs(proporcaoAtual - item.Percentual);

                if (desvio > LIMIAR_DESVIO)
                {
                    temDesvio = true;
                    break;
                }
            }

            if (temDesvio)
            {
                var vendasMes = new List<VendaRebalanceamento>();

                // Sell over-allocated
                foreach (var item in cesta.Itens)
                {
                    var custodia = custodias.FirstOrDefault(c => c.Ticker == item.Ticker);
                    if (custodia == null || custodia.Quantidade <= 0) continue;

                    var cotacao = cotacoes.ContainsKey(item.Ticker) ? cotacoes[item.Ticker] : custodia.PrecoMedio;
                    var valorAtual = custodia.Quantidade * cotacao;
                    var valorAlvo = valorTotal * (item.Percentual / 100m);

                    if (valorAtual > valorAlvo + (cotacao * 0.5m)) // threshold to avoid micro-trades
                    {
                        var excesso = (int)Math.Truncate((valorAtual - valorAlvo) / cotacao);
                        if (excesso > 0)
                        {
                            var lucro = (cotacao - custodia.PrecoMedio) * excesso;
                            var venda = new VendaRebalanceamento
                            {
                                ClienteId = cliente.Id,
                                Ticker = item.Ticker,
                                Quantidade = excesso,
                                PrecoVenda = cotacao,
                                PrecoMedio = custodia.PrecoMedio,
                                ValorVenda = excesso * cotacao,
                                Lucro = lucro,
                                DataVenda = DateTime.UtcNow
                            };
                            await _vendaRepository.AddAsync(venda);
                            vendasMes.Add(venda);

                            custodia.Quantidade -= excesso;
                            await _custodiaFilhoteRepository.UpdateAsync(custodia);
                        }
                    }
                }

                // Buy under-allocated
                foreach (var item in cesta.Itens)
                {
                    var custodia = custodias.FirstOrDefault(c => c.Ticker == item.Ticker);
                    var cotacao = cotacoes.ContainsKey(item.Ticker) ? cotacoes[item.Ticker] : 0m;
                    if (cotacao <= 0) continue;

                    var qtdAtual = custodia?.Quantidade ?? 0;
                    var valorAtual = qtdAtual * cotacao;
                    var valorAlvo = valorTotal * (item.Percentual / 100m);

                    if (valorAtual < valorAlvo - (cotacao * 0.5m))
                    {
                        var deficit = (int)Math.Truncate((valorAlvo - valorAtual) / cotacao);
                        if (deficit > 0)
                        {
                            if (custodia == null)
                            {
                                custodia = new CustodiaFilhote
                                {
                                    ClienteId = cliente.Id,
                                    Ticker = item.Ticker,
                                    Quantidade = deficit,
                                    PrecoMedio = cotacao,
                                    ValorTotalInvestido = deficit * cotacao,
                                    DataAtualizacao = DateTime.UtcNow
                                };
                                await _custodiaFilhoteRepository.AddAsync(custodia);
                            }
                            else
                            {
                                var novoPrecoMedio = ((custodia.Quantidade * custodia.PrecoMedio) + (deficit * cotacao))
                                    / (custodia.Quantidade + deficit);
                                custodia.Quantidade += deficit;
                                custodia.PrecoMedio = novoPrecoMedio;
                                custodia.ValorTotalInvestido += deficit * cotacao;
                                custodia.DataAtualizacao = DateTime.UtcNow;
                                await _custodiaFilhoteRepository.UpdateAsync(custodia);
                            }
                        }
                    }
                }

                await CalcularIRVendasAsync(cliente, vendasMes);
            }
        }
    }

    private async Task CalcularIRVendasAsync(Cliente cliente, List<VendaRebalanceamento> vendasMes)
    {
        if (vendasMes.Count == 0) return;

        var agora = DateTime.UtcNow;
        var vendasAntigasMes = await _vendaRepository.GetByClienteAndMesAsync(cliente.Id, agora.Year, agora.Month);
        var todasVendasMes = vendasAntigasMes.Concat(vendasMes).ToList();

        var totalVendasMes = todasVendasMes.Sum(v => v.ValorVenda);
        var lucroTotal = todasVendasMes.Sum(v => v.Lucro);

        if (totalVendasMes > 20000m && lucroTotal > 0)
        {
            var ir = Math.Round(lucroTotal * 0.20m, 2);

            try
            {
                await _kafkaProducer.PublicarIRVendaAsync(new
                {
                    tipo = "IR_VENDA",
                    clienteId = cliente.Id,
                    cpf = cliente.Cpf,
                    mesReferencia = agora.ToString("yyyy-MM"),
                    totalVendasMes = totalVendasMes,
                    lucroLiquido = lucroTotal,
                    aliquota = 0.20m,
                    valorIR = ir,
                    detalhes = vendasMes.Select(v => new
                    {
                        ticker = v.Ticker,
                        quantidade = v.Quantidade,
                        precoVenda = v.PrecoVenda,
                        precoMedio = v.PrecoMedio,
                        lucro = v.Lucro
                    }),
                    dataCalculo = agora
                });
            }
            catch
            {
                // Log but don't fail
            }
        }
    }
}
