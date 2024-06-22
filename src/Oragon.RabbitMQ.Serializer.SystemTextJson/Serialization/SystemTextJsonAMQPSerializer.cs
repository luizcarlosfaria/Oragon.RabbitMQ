using RabbitMQ.Client;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Oragon.RabbitMQ.Serialization;


/// <summary>
/// Implements serialization using System.Text.Json
/// </summary>
[SuppressMessage("Sonar", "S100", Justification = "AMQP is a acronym for Advanced Message Queuing Protocol, so it's a name.")]
[SuppressMessage("Sonar", "S101", Justification = "AMQP is a acronym for Advanced Message Queuing Protocol, so it's a name.")]
public class SystemTextJsonAMQPSerializer : AMQPBaseSerializer
{
    private readonly JsonSerializerOptions options;

    /// <summary>
    /// Create a instance of SystemTextJsonAMQPSerializer
    /// </summary>    
    public SystemTextJsonAMQPSerializer(JsonSerializerOptions options) : base()
    {
        this.options = options ?? new();
    }


    /// <summary>
    /// Deserialize a message using System.Text.Json
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    protected override TMessage DeserializeInternal<TMessage>(IReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body)
    {
        var bytes = body.ToArray();
        if (bytes.Length > 0)
        {
            var message = Encoding.UTF8.GetString(bytes);
            if (!string.IsNullOrWhiteSpace(message))
            {
                return JsonSerializer.Deserialize<TMessage>(message, this.options);
            }
        }
        return default;

    }

    /// <summary>
    /// Serialize a message using System.Text.Json  
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    protected override byte[] SerializeInternal<TMessage>(BasicProperties basicProperties, TMessage message)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, this.options));
    }
}

