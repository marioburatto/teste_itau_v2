using CompraProgramada.Domain.Entities;

namespace CompraProgramada.Application.Interfaces;

public interface ICotahistService
{
    IEnumerable<CotacaoB3> ParseArquivo(string caminhoArquivo);
    CotacaoB3? ObterCotacaoFechamento(string pastaCotacoes, string ticker);
    Dictionary<string, decimal> ObterCotacoesFechamento(string pastaCotacoes, IEnumerable<string> tickers);
}
