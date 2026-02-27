namespace CompraProgramada.Domain.Entities;

public class Distribuicao
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorOperacao { get; set; }
    public decimal ValorIRDedoDuro { get; set; }
    public DateTime DataDistribuicao { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}
