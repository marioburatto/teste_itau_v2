using CompraProgramada.Domain.Enums;

namespace CompraProgramada.Domain.Entities;

public class ContaGrafica
{
    public int Id { get; set; }
    public string NumeroConta { get; set; } = string.Empty;
    public TipoConta Tipo { get; set; }
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;

    public int? ClienteId { get; set; }
    public Cliente? Cliente { get; set; }
}
