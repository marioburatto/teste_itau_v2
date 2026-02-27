namespace CompraProgramada.Application.DTOs;

public class RentabilidadeResponse
{
    public int ClienteId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime DataConsulta { get; set; }
    public ResumoCarteira Rentabilidade { get; set; } = new();
    public List<HistoricoAporteDto> HistoricoAportes { get; set; } = new();
    public List<EvolucaoCarteiraDto> EvolucaoCarteira { get; set; } = new();
}

public class HistoricoAporteDto
{
    public string Data { get; set; } = string.Empty;
    public decimal Valor { get; set; }
    public string Parcela { get; set; } = string.Empty;
}

public class EvolucaoCarteiraDto
{
    public string Data { get; set; } = string.Empty;
    public decimal ValorCarteira { get; set; }
    public decimal ValorInvestido { get; set; }
    public decimal Rentabilidade { get; set; }
}
