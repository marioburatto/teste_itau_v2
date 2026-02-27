namespace CompraProgramada.Domain.Entities;

public class HistoricoAporte
{
    public int Id { get; set; }
    public int ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
    public DateTime Data { get; set; }
    public decimal Valor { get; set; }
    public string Parcela { get; set; } = string.Empty;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}
