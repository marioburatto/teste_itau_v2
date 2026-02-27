using CompraProgramada.Application.DTOs;
using CompraProgramada.Application.Interfaces;
using CompraProgramada.Domain.Entities;
using CompraProgramada.Domain.Enums;
using CompraProgramada.Domain.Interfaces;

namespace CompraProgramada.Application.Services;

public class ClienteService
{
    private readonly IClienteRepository _clienteRepository;
    private readonly ICustodiaFilhoteRepository _custodiaFilhoteRepository;
    private readonly ICotahistService _cotahistService;
    private readonly string _pastaCotacoes;

    public ClienteService(
        IClienteRepository clienteRepository,
        ICustodiaFilhoteRepository custodiaFilhoteRepository,
        ICotahistService cotahistService,
        string pastaCotacoes = "cotacoes")
    {
        _clienteRepository = clienteRepository;
        _custodiaFilhoteRepository = custodiaFilhoteRepository;
        _cotahistService = cotahistService;
        _pastaCotacoes = pastaCotacoes;
    }

    public async Task<AdesaoResponse> AderirAsync(AdesaoRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Nome))
            throw new BusinessException("Nome e obrigatorio.", "DADOS_INVALIDOS");

        if (string.IsNullOrWhiteSpace(request.Cpf))
            throw new BusinessException("CPF e obrigatorio.", "DADOS_INVALIDOS");

        if (string.IsNullOrWhiteSpace(request.Email))
            throw new BusinessException("Email e obrigatorio.", "DADOS_INVALIDOS");

        if (request.ValorMensal < 100m)
            throw new BusinessException("O valor mensal minimo e de R$ 100,00.", "VALOR_MENSAL_INVALIDO");

        var existente = await _clienteRepository.GetByCpfAsync(request.Cpf);
        if (existente != null)
            throw new BusinessException("CPF ja cadastrado no sistema.", "CLIENTE_CPF_DUPLICADO");

        var cliente = new Cliente
        {
            Nome = request.Nome,
            Cpf = request.Cpf,
            Email = request.Email,
            ValorMensal = request.ValorMensal,
            Ativo = true,
            DataAdesao = DateTime.UtcNow
        };

        cliente = await _clienteRepository.AddAsync(cliente);

        var conta = new ContaGrafica
        {
            NumeroConta = $"FLH-{cliente.Id:D6}",
            Tipo = TipoConta.FILHOTE,
            DataCriacao = DateTime.UtcNow,
            ClienteId = cliente.Id
        };
        cliente.ContaGrafica = conta;
        await _clienteRepository.UpdateAsync(cliente);

        return new AdesaoResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            Cpf = cliente.Cpf,
            Email = cliente.Email,
            ValorMensal = cliente.ValorMensal,
            Ativo = true,
            DataAdesao = cliente.DataAdesao,
            ContaGrafica = new ContaGraficaDto
            {
                Id = conta.Id,
                NumeroConta = conta.NumeroConta,
                Tipo = "FILHOTE",
                DataCriacao = conta.DataCriacao
            }
        };
    }

    public async Task<SaidaResponse> SairAsync(int clienteId)
    {
        var cliente = await _clienteRepository.GetByIdAsync(clienteId);
        if (cliente == null)
            throw new NotFoundException("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO");

        if (!cliente.Ativo)
            throw new BusinessException("Cliente ja havia saido do produto.", "CLIENTE_JA_INATIVO");

        cliente.Ativo = false;
        cliente.DataSaida = DateTime.UtcNow;
        await _clienteRepository.UpdateAsync(cliente);

        return new SaidaResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            Ativo = false,
            DataSaida = cliente.DataSaida,
            Mensagem = "Adesao encerrada. Sua posicao em custodia foi mantida."
        };
    }

    public async Task<AlterarValorMensalResponse> AlterarValorMensalAsync(int clienteId, AlterarValorMensalRequest request)
    {
        var cliente = await _clienteRepository.GetByIdAsync(clienteId);
        if (cliente == null)
            throw new NotFoundException("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO");

        if (request.NovoValorMensal < 100m)
            throw new BusinessException("O valor mensal minimo e de R$ 100,00.", "VALOR_MENSAL_INVALIDO");

        var valorAnterior = cliente.ValorMensal;
        cliente.ValorMensal = request.NovoValorMensal;

        cliente.HistoricoValoresMensais.Add(new HistoricoValorMensal
        {
            ClienteId = cliente.Id,
            ValorAnterior = valorAnterior,
            ValorNovo = request.NovoValorMensal,
            DataAlteracao = DateTime.UtcNow
        });

        await _clienteRepository.UpdateAsync(cliente);

        return new AlterarValorMensalResponse
        {
            ClienteId = cliente.Id,
            ValorMensalAnterior = valorAnterior,
            ValorMensalNovo = request.NovoValorMensal,
            DataAlteracao = DateTime.UtcNow,
            Mensagem = "Valor mensal atualizado. O novo valor sera considerado a partir da proxima data de compra."
        };
    }

    public async Task<CarteiraResponse> ConsultarCarteiraAsync(int clienteId)
    {
        var cliente = await _clienteRepository.GetByIdAsync(clienteId);
        if (cliente == null)
            throw new NotFoundException("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO");

        var custodias = await _custodiaFilhoteRepository.GetByClienteIdAsync(clienteId);
        var tickers = custodias.Select(c => c.Ticker).ToList();

        var cotacoes = _cotahistService.ObterCotacoesFechamento(_pastaCotacoes, tickers);

        var ativos = new List<AtivoCarteiraDto>();
        decimal valorTotalAtual = 0;
        decimal valorTotalInvestido = 0;

        foreach (var custodia in custodias)
        {
            var cotacaoAtual = cotacoes.ContainsKey(custodia.Ticker) ? cotacoes[custodia.Ticker] : custodia.PrecoMedio;
            var valorAtual = custodia.Quantidade * cotacaoAtual;
            var pl = (cotacaoAtual - custodia.PrecoMedio) * custodia.Quantidade;
            var plPercentual = custodia.PrecoMedio > 0 ? ((cotacaoAtual - custodia.PrecoMedio) / custodia.PrecoMedio) * 100m : 0m;

            valorTotalAtual += valorAtual;
            valorTotalInvestido += custodia.Quantidade * custodia.PrecoMedio;

            ativos.Add(new AtivoCarteiraDto
            {
                Ticker = custodia.Ticker,
                Quantidade = custodia.Quantidade,
                PrecoMedio = Math.Round(custodia.PrecoMedio, 2),
                CotacaoAtual = Math.Round(cotacaoAtual, 2),
                ValorAtual = Math.Round(valorAtual, 2),
                Pl = Math.Round(pl, 2),
                PlPercentual = Math.Round(plPercentual, 2),
                ComposicaoCarteira = 0 // calculated below
            });
        }

        // Calculate portfolio composition percentages
        foreach (var ativo in ativos)
        {
            ativo.ComposicaoCarteira = valorTotalAtual > 0
                ? Math.Round((ativo.ValorAtual / valorTotalAtual) * 100m, 2)
                : 0m;
        }

        var plTotal = valorTotalAtual - valorTotalInvestido;
        var rentabilidade = valorTotalInvestido > 0
            ? ((valorTotalAtual - valorTotalInvestido) / valorTotalInvestido) * 100m
            : 0m;

        return new CarteiraResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            ContaGrafica = cliente.ContaGrafica?.NumeroConta ?? "",
            DataConsulta = DateTime.UtcNow,
            Resumo = new ResumoCarteira
            {
                ValorTotalInvestido = Math.Round(valorTotalInvestido, 2),
                ValorAtualCarteira = Math.Round(valorTotalAtual, 2),
                PlTotal = Math.Round(plTotal, 2),
                RentabilidadePercentual = Math.Round(rentabilidade, 2)
            },
            Ativos = ativos
        };
    }

    public async Task<RentabilidadeResponse> ConsultarRentabilidadeAsync(int clienteId)
    {
        var cliente = await _clienteRepository.GetByIdAsync(clienteId);
        if (cliente == null)
            throw new NotFoundException("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO");

        var carteira = await ConsultarCarteiraAsync(clienteId);

        var historicoAportes = cliente.HistoricoAportes
            .OrderBy(h => h.Data)
            .Select(h => new HistoricoAporteDto
            {
                Data = h.Data.ToString("yyyy-MM-dd"),
                Valor = h.Valor,
                Parcela = h.Parcela
            })
            .ToList();

        // Build portfolio evolution from aportes history
        var evolucao = new List<EvolucaoCarteiraDto>();
        decimal acumuladoInvestido = 0;
        foreach (var aporte in cliente.HistoricoAportes.OrderBy(h => h.Data))
        {
            acumuladoInvestido += aporte.Valor;
            evolucao.Add(new EvolucaoCarteiraDto
            {
                Data = aporte.Data.ToString("yyyy-MM-dd"),
                ValorInvestido = Math.Round(acumuladoInvestido, 2),
                ValorCarteira = Math.Round(acumuladoInvestido, 2), // simplified: actual portfolio value at the time
                Rentabilidade = 0
            });
        }

        // Update last entry with current values
        if (evolucao.Count > 0)
        {
            var last = evolucao.Last();
            last.ValorCarteira = carteira.Resumo.ValorAtualCarteira;
            last.ValorInvestido = carteira.Resumo.ValorTotalInvestido;
            last.Rentabilidade = carteira.Resumo.RentabilidadePercentual;
        }

        return new RentabilidadeResponse
        {
            ClienteId = cliente.Id,
            Nome = cliente.Nome,
            DataConsulta = DateTime.UtcNow,
            Rentabilidade = carteira.Resumo,
            HistoricoAportes = historicoAportes,
            EvolucaoCarteira = evolucao
        };
    }
}

public class BusinessException : Exception
{
    public string Codigo { get; }
    public BusinessException(string message, string codigo) : base(message)
    {
        Codigo = codigo;
    }
}

public class NotFoundException : Exception
{
    public string Codigo { get; }
    public NotFoundException(string message, string codigo) : base(message)
    {
        Codigo = codigo;
    }
}
