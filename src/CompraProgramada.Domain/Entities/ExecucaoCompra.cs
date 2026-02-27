namespace CompraProgramada.Domain.Entities;

public class ExecucaoCompra
{
    public int Id { get; set; }
    public DateTime DataReferencia { get; set; }
    public DateTime DataExecucao { get; set; } = DateTime.UtcNow;
    public int TotalClientes { get; set; }
    public decimal TotalConsolidado { get; set; }
    public string Parcela { get; set; } = string.Empty;
    public bool Concluida { get; set; }
}
