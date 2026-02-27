namespace CompraProgramada.Application.Interfaces;

public interface IKafkaProducer
{
    Task PublicarIRDedoDuroAsync(object mensagem);
    Task PublicarIRVendaAsync(object mensagem);
}
