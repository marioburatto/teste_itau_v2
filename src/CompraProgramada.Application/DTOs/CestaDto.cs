namespace CompraProgramada.Application.DTOs;

public class CestaRequest
{
    public string Nome { get; set; } = string.Empty;
    public List<CestaItemRequest> Itens { get; set; } = new();
}

public class CestaItemRequest
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
}

public class CestaResponse
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public List<CestaItemResponse> Itens { get; set; } = new();
    public bool RebalanceamentoDisparado { get; set; }
    public CestaDesativadaDto? CestaAnteriorDesativada { get; set; }
    public List<string>? AtivosRemovidos { get; set; }
    public List<string>? AtivosAdicionados { get; set; }
    public string Mensagem { get; set; } = string.Empty;
}

public class CestaItemResponse
{
    public string Ticker { get; set; } = string.Empty;
    public decimal Percentual { get; set; }
    public decimal? CotacaoAtual { get; set; }
}

public class CestaDesativadaDto
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public DateTime? DataDesativacao { get; set; }
}

public class CestaAtualResponse
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public List<CestaItemResponse> Itens { get; set; } = new();
}

public class HistoricoCestasResponse
{
    public List<CestaHistoricoDto> Cestas { get; set; } = new();
}

public class CestaHistoricoDto
{
    public int CestaId { get; set; }
    public string Nome { get; set; } = string.Empty;
    public bool Ativa { get; set; }
    public DateTime DataCriacao { get; set; }
    public DateTime? DataDesativacao { get; set; }
    public List<CestaItemResponse> Itens { get; set; } = new();
}
