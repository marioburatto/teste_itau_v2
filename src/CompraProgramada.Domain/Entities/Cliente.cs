namespace CompraProgramada.Domain.Entities;

public class Cliente
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string Cpf { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public decimal ValorMensal { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataAdesao { get; set; } = DateTime.UtcNow;
    public DateTime? DataSaida { get; set; }

    public ContaGrafica? ContaGrafica { get; set; }
    public List<CustodiaFilhote> CustodiaFilhote { get; set; } = new();
    public List<HistoricoAporte> HistoricoAportes { get; set; } = new();
    public List<HistoricoValorMensal> HistoricoValoresMensais { get; set; } = new();
}
