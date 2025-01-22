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
/// Create a instance of SystemTextJsonAmqpSerializer
/// </remarks>    
[SuppressMessage("Sonar", "S100", Justification = "Amqp is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
[SuppressMessage("Sonar", "S101", Justification = "Amqp is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
public class SystemTextJsonAmqpSerializer(JsonSerializerOptions options) : IAmqpSerializer
{
    private readonly JsonSerializerOptions options = options ?? new();


    /// <summary>
    /// Deserialize a message using System.Text.Json
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="eventArgs"></param>    
    /// <returns></returns>
    public TMessage Deserialize<TMessage>(BasicDeliverEventArgs eventArgs)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);


        var bytes = eventArgs.Body.ToArray();
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
    /// <param name="eventArgs"></param>
    /// <param name="type"></param>    
    /// <returns></returns>
    public object Deserialize(BasicDeliverEventArgs eventArgs, Type type)
    {
        ArgumentNullException.ThrowIfNull(eventArgs);
        ArgumentNullException.ThrowIfNull(type);

        var bytes = eventArgs.Body.ToArray();
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
        ArgumentNullException.ThrowIfNull(basicProperties);

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
        ArgumentNullException.ThrowIfNull(basicProperties);

        return Encoding.UTF8.GetBytes(JsonSerializer.Serialize(message, this.options));
    }
}

