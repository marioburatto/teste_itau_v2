using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Interfaces;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Enums;
using CompraProgramada.Domain.Interfaces;

namespace CompraProgramada.Application.Services;

public class MotorCompraService
{
    private readonly IClienteRepository _clienteRepository;
    private readonly ICestaRepository _cestaRepository;
    private readonly ICustodiaFilhoteRepository _custodiaFilhoteRepository;
    private readonly ICustodiaMasterRepository _custodiaMasterRepository;
    private readonly IOrdemCompraRepository _ordemCompraRepository;
    private readonly IDistribuicaoRepository _distribuicaoRepository;
    private readonly IExecucaoCompraRepository _execucaoCompraRepository;
    private readonly ICotahistService _cotahistService;
    private readonly IKafkaProducer _kafkaProducer;
    private readonly string _pastaCotacoes;

    public MotorCompraService(
        IClienteRepository clienteRepository,
        ICestaRepository cestaRepository,
        ICustodiaFilhoteRepository custodiaFilhoteRepository,
        ICustodiaMasterRepository custodiaMasterRepository,
        IOrdemCompraRepository ordemCompraRepository,
        IDistribuicaoRepository distribuicaoRepository,
        IExecucaoCompraRepository execucaoCompraRepository,
        ICotahistService cotahistService,
        IKafkaProducer kafkaProducer,
        string pastaCotacoes = "cotacoes")
    {
        _clienteRepository = clienteRepository;
        _cestaRepository = cestaRepository;
        _custodiaFilhoteRepository = custodiaFilhoteRepository;
        _custodiaMasterRepository = custodiaMasterRepository;
        _ordemCompraRepository = ordemCompraRepository;
        _distribuicaoRepository = distribuicaoRepository;
        _execucaoCompraRepository = execucaoCompraRepository;
        _cotahistService = cotahistService;
        _kafkaProducer = kafkaProducer;
        _pastaCotacoes = pastaCotacoes;
    }

    public static DateTime ObterProximaDataCompra(DateTime data)
    {
        int[] diasAlvo = { 5, 15, 25 };
        var candidatos = diasAlvo
            .Select(d =>
            {
                var dt = new DateTime(data.Year, data.Month, d);
                while (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
                    dt = dt.AddDays(1);
                return dt;
            })
            .Where(dt => dt >= data.Date)
            .OrderBy(dt => dt)
            .ToList();

        return candidatos.FirstOrDefault() != default
            ? candidatos.First()
            : ObterProximaDataCompra(new DateTime(data.Year, data.Month, 1).AddMonths(1));
    }

    public static string DeterminarParcela(DateTime dataReferencia)
    {
        if (dataReferencia.Day <= 10) return "1/3";
        if (dataReferencia.Day <= 20) return "2/3";
        return "3/3";
    }

    public async Task<ExecutarCompraResponse> ExecutarCompraAsync(DateTime dataReferencia)
    {
        // Check if already executed for this date
        var execucaoExistente = await _execucaoCompraRepository.GetByDataReferenciaAsync(dataReferencia.Date);
        if (execucaoExistente != null && execucaoExistente.Concluida)
            throw new BusinessException("Compra ja foi executada para esta data.", "COMPRA_JA_EXECUTADA");

        // Get active basket
        var cesta = await _cestaRepository.GetAtivaAsync();
        if (cesta == null)
            throw new NotFoundException("Nenhuma cesta ativa encontrada.", "CESTA_NAO_ENCONTRADA");

        // Step 1: Collect active clients and calculate 1/3 monthly contribution
        var clientesAtivos = await _clienteRepository.GetAtivosAsync();
        if (clientesAtivos.Count == 0)
            throw new BusinessException("Nenhum cliente ativo encontrado.", "SEM_CLIENTES_ATIVOS");

        var parcela = DeterminarParcela(dataReferencia);
        var aportesPorCliente = clientesAtivos.ToDictionary(c => c, c => Math.Round(c.ValorMensal / 3m, 2));
        var totalConsolidado = aportesPorCliente.Values.Sum();

        // Step 2: Get closing prices from COTAHIST
        var tickers = cesta.Itens.Select(i => i.Ticker).ToList();
        var cotacoes = _cotahistService.ObterCotacoesFechamento(_pastaCotacoes, tickers);

        if (cotacoes.Count == 0)
            throw new NotFoundException("Arquivo COTAHIST nao encontrado para a data.", "COTACAO_NAO_ENCONTRADA");

        // Step 3: Calculate quantities per asset
        var quantidadesPorAtivo = new Dictionary<string, int>();
        foreach (var item in cesta.Itens)
        {
            if (!cotacoes.ContainsKey(item.Ticker))
                throw new NotFoundException($"Cotacao nao encontrada para {item.Ticker}.", "COTACAO_NAO_ENCONTRADA");

            var valorParaAtivo = totalConsolidado * (item.Percentual / 100m);
            var cotacao = cotacoes[item.Ticker];
            var quantidade = (int)Math.Truncate(valorParaAtivo / cotacao);
            quantidadesPorAtivo[item.Ticker] = quantidade;
        }

        // Step 4: Check master custody balance (residuals)
        var saldoMaster = await _custodiaMasterRepository.GetAllAsync();
        var quantidadesAComprar = new Dictionary<string, int>();
        var totalDisponivel = new Dictionary<string, int>();

        foreach (var ticker in tickers)
        {
            var saldo = saldoMaster.FirstOrDefault(s => s.Ticker == ticker);
            var saldoQtd = saldo?.Quantidade ?? 0;
            totalDisponivel[ticker] = quantidadesPorAtivo[ticker]; // total needed
            var aComprar = Math.Max(0, quantidadesPorAtivo[ticker] - saldoQtd);
            quantidadesAComprar[ticker] = aComprar;

            // After using master custody, set it to 0 (will be recalculated with residuals)
            if (saldo != null)
            {
                totalDisponivel[ticker] = quantidadesPorAtivo[ticker]; // total = wanted amount
                saldo.Quantidade = 0;
                await _custodiaMasterRepository.UpdateAsync(saldo);
            }
        }

        // Step 5: Execute purchases (lot standard + fractional)
        var ordensCompra = new List<OrdemCompraDto>();
        foreach (var ticker in tickers)
        {
            var qtdComprar = quantidadesAComprar[ticker];
            if (qtdComprar <= 0) continue;

            var lotePadrao = (qtdComprar / 100) * 100;
            var fracionario = qtdComprar % 100;
            var preco = cotacoes[ticker];

            var ordem = new OrdemCompra
            {
                DataExecucao = dataReferencia,
                Ticker = ticker,
                QuantidadeTotal = qtdComprar,
                QuantidadeLotePadrao = lotePadrao,
                QuantidadeFracionario = fracionario,
                PrecoUnitario = preco,
                ValorTotal = qtdComprar * preco,
                TipoOperacao = TipoOperacao.COMPRA
            };
            await _ordemCompraRepository.AddAsync(ordem);

            var detalhes = new List<DetalheOrdemDto>();
            if (lotePadrao > 0)
            {
                detalhes.Add(new DetalheOrdemDto
                {
                    Tipo = "LOTE_PADRAO",
                    Ticker = ticker,
                    Quantidade = lotePadrao
                });
            }
            if (fracionario > 0)
            {
                detalhes.Add(new DetalheOrdemDto
                {
                    Tipo = "FRACIONARIO",
                    Ticker = ticker + "F",
                    Quantidade = fracionario
                });
            }

            ordensCompra.Add(new OrdemCompraDto
            {
                Ticker = ticker,
                QuantidadeTotal = qtdComprar,
                Detalhes = detalhes,
                PrecoUnitario = preco,
                ValorTotal = qtdComprar * preco
            });
        }

        // Step 6: Distribute to child accounts proportionally
        var distribuicoes = new List<DistribuicaoClienteDto>();
        var residuos = new Dictionary<string, int>();
        int eventosIR = 0;

        foreach (var ticker in tickers)
        {
            var qtdTotal = quantidadesPorAtivo[ticker]; // total available (purchased + master residual)
            residuos[ticker] = qtdTotal;
        }

        foreach (var kvp in aportesPorCliente)
        {
            var cliente = kvp.Key;
            var valorAporte = kvp.Value;
            var proporcao = totalConsolidado > 0 ? valorAporte / totalConsolidado : 0m;

            var ativosDistribuidos = new List<AtivoDistribuidoDto>();

            foreach (var ticker in tickers)
            {
                var qtdTotal = quantidadesPorAtivo[ticker];
                var qtdCliente = (int)Math.Truncate(qtdTotal * proporcao);
                residuos[ticker] -= qtdCliente;

                if (qtdCliente > 0)
                {
                    var preco = cotacoes[ticker];
                    var valorOperacao = qtdCliente * preco;
                    var irDedoDuro = Math.Round(valorOperacao * 0.00005m, 2);

                    // Save distribution record
                    await _distribuicaoRepository.AddAsync(new Distribuicao
                    {
                        ClienteId = cliente.Id,
                        Ticker = ticker,
                        Quantidade = qtdCliente,
                        PrecoUnitario = preco,
                        ValorOperacao = valorOperacao,
                        ValorIRDedoDuro = irDedoDuro,
                        DataDistribuicao = dataReferencia
                    });

                    // Update child custody and average price
                    var custodiaFilhote = await _custodiaFilhoteRepository.GetByClienteAndTickerAsync(cliente.Id, ticker);
                    if (custodiaFilhote == null)
                    {
                        custodiaFilhote = new CustodiaFilhote
                        {
                            ClienteId = cliente.Id,
                            Ticker = ticker,
                            Quantidade = qtdCliente,
                            PrecoMedio = preco,
                            ValorTotalInvestido = valorOperacao,
                            DataAtualizacao = DateTime.UtcNow
                        };
                        await _custodiaFilhoteRepository.AddAsync(custodiaFilhote);
                    }
                    else
                    {
                        // Recalculate average price
                        var novoPrecoMedio = ((custodiaFilhote.Quantidade * custodiaFilhote.PrecoMedio) + (qtdCliente * preco))
                            / (custodiaFilhote.Quantidade + qtdCliente);
                        custodiaFilhote.Quantidade += qtdCliente;
                        custodiaFilhote.PrecoMedio = novoPrecoMedio;
                        custodiaFilhote.ValorTotalInvestido += valorOperacao;
                        custodiaFilhote.DataAtualizacao = DateTime.UtcNow;
                        await _custodiaFilhoteRepository.UpdateAsync(custodiaFilhote);
                    }

                    // Publish IR dedo-duro to Kafka
                    try
                    {
                        await _kafkaProducer.PublicarIRDedoDuroAsync(new
                        {
                            tipo = "IR_DEDO_DURO",
                            clienteId = cliente.Id,
                            cpf = cliente.Cpf,
                            ticker = ticker,
                            tipoOperacao = "COMPRA",
                            quantidade = qtdCliente,
                            precoUnitario = preco,
                            valorOperacao = valorOperacao,
                            aliquota = 0.00005m,
                            valorIR = irDedoDuro,
                            dataOperacao = dataReferencia
                        });
                        eventosIR++;
                    }
                    catch
                    {
                        // Log but don't fail the operation
                    }

                    ativosDistribuidos.Add(new AtivoDistribuidoDto
                    {
                        Ticker = ticker,
                        Quantidade = qtdCliente
                    });
                }
            }

            // Record aporte history
            cliente.HistoricoAportes.Add(new HistoricoAporte
            {
                ClienteId = cliente.Id,
                Data = dataReferencia,
                Valor = valorAporte,
                Parcela = parcela
            });
            await _clienteRepository.UpdateAsync(cliente);

            distribuicoes.Add(new DistribuicaoClienteDto
            {
                ClienteId = cliente.Id,
                Nome = cliente.Nome,
                ValorAporte = valorAporte,
                Ativos = ativosDistribuidos
            });
        }

        // Step 7: Save residuals to master custody
        var residuosList = new List<ResiduoDto>();
        foreach (var ticker in tickers)
        {
            if (residuos[ticker] > 0)
            {
                var custodiaMaster = await _custodiaMasterRepository.GetByTickerAsync(ticker);
                if (custodiaMaster == null)
                {
                    await _custodiaMasterRepository.AddAsync(new CustodiaMaster
                    {
                        Ticker = ticker,
                        Quantidade = residuos[ticker],
                        PrecoMedio = cotacoes[ticker],
                        Origem = $"Residuo distribuicao {dataReferencia:yyyy-MM-dd}",
                        DataAtualizacao = DateTime.UtcNow
                    });
                }
                else
                {
                    custodiaMaster.Quantidade += residuos[ticker];
                    custodiaMaster.Origem = $"Residuo distribuicao {dataReferencia:yyyy-MM-dd}";
                    custodiaMaster.DataAtualizacao = DateTime.UtcNow;
                    await _custodiaMasterRepository.UpdateAsync(custodiaMaster);
                }

                residuosList.Add(new ResiduoDto
                {
                    Ticker = ticker,
                    Quantidade = residuos[ticker]
                });
            }
        }

        // Record execution
        var execucao = new ExecucaoCompra
        {
            DataReferencia = dataReferencia.Date,
            DataExecucao = DateTime.UtcNow,
            TotalClientes = clientesAtivos.Count,
            TotalConsolidado = totalConsolidado,
            Parcela = parcela,
            Concluida = true
        };
        await _execucaoCompraRepository.AddAsync(execucao);

        return new ExecutarCompraResponse
        {
            DataExecucao = DateTime.UtcNow,
            TotalClientes = clientesAtivos.Count,
            TotalConsolidado = totalConsolidado,
            OrdensCompra = ordensCompra,
            Distribuicoes = distribuicoes,
            ResiduosCustMaster = residuosList,
            EventosIRPublicados = eventosIR,
            Mensagem = $"Compra programada executada com sucesso para {clientesAtivos.Count} clientes."
        };
    }

    public async Task<CustodiaMasterResponse> ConsultarCustodiaMasterAsync()
    {
        var custodias = await _custodiaMasterRepository.GetAllAsync();
        var tickers = custodias.Where(c => c.Quantidade > 0).Select(c => c.Ticker).ToList();
        var cotacoes = _cotahistService.ObterCotacoesFechamento(_pastaCotacoes, tickers);

        var items = custodias.Where(c => c.Quantidade > 0).Select(c => new CustodiaMasterItemDto
        {
            Ticker = c.Ticker,
            Quantidade = c.Quantidade,
            PrecoMedio = Math.Round(c.PrecoMedio, 2),
            ValorAtual = cotacoes.ContainsKey(c.Ticker)
                ? Math.Round(c.Quantidade * cotacoes[c.Ticker], 2)
                : Math.Round(c.Quantidade * c.PrecoMedio, 2),
            Origem = c.Origem
        }).ToList();

        return new CustodiaMasterResponse
        {
            ContaMaster = new ContaMasterDto(),
            Custodia = items,
            ValorTotalResiduo = items.Sum(i => i.ValorAtual)
        };
    }
}
