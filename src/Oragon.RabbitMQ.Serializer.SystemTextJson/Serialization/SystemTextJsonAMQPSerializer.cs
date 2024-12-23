using Dawn;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using System.Text.Json;

namespace Oragon.RabbitMQ.Serialization;


/// <summary>
/// Implements serialization using System.Text.Json
/// </summary>
/// <remarks>
/// Create a instance of SystemTextJsonAMQPSerializer
/// </remarks>    
[SuppressMessage("Sonar", "S100", Justification = "AMQP is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
[SuppressMessage("Sonar", "S101", Justification = "AMQP is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
public class SystemTextJsonAMQPSerializer(JsonSerializerOptions options) : IAMQPSerializer
{
    private readonly JsonSerializerOptions options = options ?? new();


    /// <summary>
    /// Deserialize a message using System.Text.Json
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicDeliver"></param>    
    /// <returns></returns>
    public TMessage Deserialize<TMessage>(BasicDeliverEventArgs basicDeliver)
    {
        _ = Guard.Argument(basicDeliver).NotNull();

        var bytes = basicDeliver.Body.ToArray();
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
    /// Deserialize a message using System.Text.Json
    /// </summary>
    /// <param name="basicDeliver"></param>
    /// <param name="type"></param>    
    /// <returns></returns>
    public object Deserialize(BasicDeliverEventArgs basicDeliver, Type type)
    {
        _ = Guard.Argument(basicDeliver).NotNull();
        _ = Guard.Argument(type).NotNull();

        var bytes = basicDeliver.Body.ToArray();
        if (bytes.Length > 0)
        {
            var message = Encoding.UTF8.GetString(bytes);
            if (!string.IsNullOrWhiteSpace(message))
            {
                return JsonSerializer.Deserialize(message, type, this.options);
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
    public byte[] Serialize<TMessage>(BasicProperties basicProperties, TMessage message)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, this.options));
    }

    /// <summary>
    /// Serialize a message using System.Text.Json  
    /// </summary>    
    /// <param name="basicProperties"></param>
    /// <param name="message"></param>
    /// <returns></returns>
    public byte[] Serialize(BasicProperties basicProperties, object message)
    {
        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, this.options));
    }
}

