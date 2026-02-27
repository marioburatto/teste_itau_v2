namespace CompraProgramada.Domain.Entities;

public class CestaItem
{
    public int Id { get; set; }
    public int CestaRecomendacaoId { get; set; }
    public CestaRecomendacao? CestaRecomendacao { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
}
