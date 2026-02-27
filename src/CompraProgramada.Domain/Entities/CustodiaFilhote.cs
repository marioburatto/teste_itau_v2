namespace CompraProgramada.Domain.Entities;

public class CustodiaFilhote
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int Quantidade { get; set; }
    public decimal PrecoMedio { get; set; }
    public decimal ValorTotalInvestido { get; set; }
    public DateTime DataAtualizacao { get; set; } = DateTime.UtcNow;
}
