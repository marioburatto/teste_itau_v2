using CompraProgramada.Domain.Enums;

namespace CompraProgramada.Domain.Entities;

public class OrdemCompra
{
    public int Id { get; set; }
    public DateTime DataExecucao { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public int QuantidadeTotal { get; set; }
    public int QuantidadeLotePadrao { get; set; }
    public int QuantidadeFracionario { get; set; }
    public decimal PrecoUnitario { get; set; }
    public decimal ValorTotal { get; set; }
    public TipoOperacao TipoOperacao { get; set; } = TipoOperacao.COMPRA;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}
