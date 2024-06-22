using Dawn;
using Newtonsoft.Json;
using RabbitMQ.Client;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Oragon.RabbitMQ.Serialization;

/// <summary>
/// Implements serialization using Newtonsoft.Json
/// </summary>
[SuppressMessage("Sonar", "S100", Justification = "AMQP is a acronym for Advanced Message Queuing Protocol, so it's a name.")]
[SuppressMessage("Sonar", "S101", Justification = "AMQP is a acronym for Advanced Message Queuing Protocol, so it's a name.")]
public class NewtonsoftAMQPSerializer : AMQPBaseSerializer
{
    private readonly JsonSerializerSettings settings;

    /// <summary>
    /// Create a instance of NewtonsoftAMQPSerializer
    /// </summary>    
    public NewtonsoftAMQPSerializer(JsonSerializerSettings settings) : base()
    {
        this.settings = settings ?? null;
    }

    /// <summary>
    /// Implement deserialization using Newtonsoft.Json
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    protected override TMessage DeserializeInternal<TMessage>(IReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body)
    {
        _ = Guard.Argument(basicProperties).NotNull();

        var bytes = body.ToArray();
        if (bytes.Length > 0)
        {
            var message = Encoding.UTF8.GetString(bytes);
            if (!string.IsNullOrWhiteSpace(message))
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<TMessage>(message, this.settings);
            }
        }
        return default;
    }

    /// <summary>
    /// Implement Serialization with Newtonsoft.Json
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    protected override byte[] SerializeInternal<TMessage>(BasicProperties basicProperties, TMessage message)
    {
        return Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(message, this.settings));
    }
}
