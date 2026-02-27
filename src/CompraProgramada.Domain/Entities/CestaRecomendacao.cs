namespace CompraProgramada.Domain.Entities;

public class CestaRecomendacao
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; } = true;
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    public DateTime? DataDesativacao { get; set; }

    public List<CestaItem> Itens { get; set; } = new();
}
