using Dawn;
using RabbitMQ.Client;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;

namespace Oragon.RabbitMQ.Serialization;

/// <summary>
/// Implements serialization using Newtonsoft.Json
/// </summary>
[SuppressMessage("Sonar", "S100", Justification = "AMQP is a acronym for Advanced Message Queuing Protocol, so it's a name.")]
public class NewtonsoftAMQPSerializer : AMQPBaseSerializer
{

    /// <summary>
    /// Create a instance of NewtonsoftAMQPSerializer
    /// </summary>
    /// <param name="activitySource"></param>
    public NewtonsoftAMQPSerializer(ActivitySource activitySource) : base(activitySource, nameof(NewtonsoftAMQPSerializer)) { }

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
                return Newtonsoft.Json.JsonConvert.DeserializeObject<TMessage>(message);
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
        return Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(message));
    }
}
