using CompraProgramada.Application.Interfaces;

namespace CompraProgramada.Tests.Helpers;

public class MockKafkaProducer : IKafkaProducer
{
    public List<object> MensagensIRDedoDuro { get; } = new();
    public List<object> MensagensIRVenda { get; } = new();

    public Task PublicarIRDedoDuroAsync(object mensagem)
    {
        MensagensIRDedoDuro.Add(mensagem);
        return Task.CompletedTask;
    }

    public Task PublicarIRVendaAsync(object mensagem)
    {
        MensagensIRVenda.Add(mensagem);
        return Task.CompletedTask;
    }
}
