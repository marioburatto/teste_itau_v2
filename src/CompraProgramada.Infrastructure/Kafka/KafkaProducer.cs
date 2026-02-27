using System.Text.Json;
using CompraProgramada.Application.Interfaces;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace CompraProgramada.Infrastructure.Kafka;

public class KafkaProducer : IKafkaProducer, IDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducer> _logger;
    private const string TopicIRDedoDuro = "ir-dedo-duro";
    private const string TopicIRVenda = "ir-venda";

    public KafkaProducer(string bootstrapServers, ILogger<KafkaProducer> logger)
    {
        _logger = logger;
        var config = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.All,
            MessageTimeoutMs = 5000
        };
        _producer = new ProducerBuilder<string, string>(config).Build();
    }

    public async Task PublicarIRDedoDuroAsync(object mensagem)
    {
        var json = JsonSerializer.Serialize(mensagem);
        _logger.LogInformation("Publicando IR Dedo-Duro no Kafka: {Mensagem}", json);

        try
        {
            var result = await _producer.ProduceAsync(TopicIRDedoDuro, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = json
            });
            _logger.LogInformation("IR Dedo-Duro publicado com sucesso. Offset: {Offset}", result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Erro ao publicar IR Dedo-Duro no Kafka");
            throw;
        }
    }

    public async Task PublicarIRVendaAsync(object mensagem)
    {
        var json = JsonSerializer.Serialize(mensagem);
        _logger.LogInformation("Publicando IR Venda no Kafka: {Mensagem}", json);

        try
        {
            var result = await _producer.ProduceAsync(TopicIRVenda, new Message<string, string>
            {
                Key = Guid.NewGuid().ToString(),
                Value = json
            });
            _logger.LogInformation("IR Venda publicado com sucesso. Offset: {Offset}", result.Offset);
        }
        catch (ProduceException<string, string> ex)
        {
            _logger.LogError(ex, "Erro ao publicar IR Venda no Kafka");
            throw;
        }
    }

    public void Dispose()
    {
        _producer?.Dispose();
    }
}
