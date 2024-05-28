using Dawn;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Diagnostics;

namespace Oragon.RabbitMQ.Serialization;

/// <summary>
/// Base serializer for AMQP implementation with OpenTelemetry support
/// </summary>
public abstract class AMQPBaseSerializer : IAMQPSerializer
{
    private readonly ActivitySource activitySource;
    private readonly string name;


    /// <summary>
    /// Create a new instance of AMQPBaseSerializer
    /// </summary>
    /// <param name="activitySource"></param>
    /// <param name="name"></param>
    protected AMQPBaseSerializer(ActivitySource activitySource, string name)
    {
        this.activitySource = activitySource;
        this.name = name;
    }

    /// <summary>
    /// Enable extension to deserialize the message
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="body"></param>
    /// <returns></returns>
    protected abstract TMessage DeserializeInternal<TMessage>(IReadOnlyBasicProperties basicProperties, ReadOnlyMemory<byte> body);


    /// <summary>
    /// Enable extension to serialize the message
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="objectToSerialize"></param>
    /// <returns></returns>
    protected abstract byte[] SerializeInternal<T>(IBasicProperties basicProperties, T objectToSerialize);


    /// <summary>
    /// Desserialize a mesage from a BasicDeliverEventArgs with OpenTelemetry support
    /// </summary>
    /// <typeparam name="TMessage"></typeparam>
    /// <param name="eventArgs"></param>
    /// <returns></returns>
    public TMessage Deserialize<TMessage>(BasicDeliverEventArgs eventArgs)
    {
        _ = Guard.Argument(eventArgs).NotNull();
        _ = Guard.Argument((IReadOnlyBasicProperties)eventArgs.BasicProperties).NotNull();

        var returnValue = this.DeserializeInternal<TMessage>(eventArgs.BasicProperties, eventArgs.Body);

        return returnValue;
    }

    /// <summary>
    /// Serialize a message to a byte array with OpenTelemetry support
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="basicProperties"></param>
    /// <param name="objectToSerialize"></param>
    /// <returns></returns>
    public byte[] Serialize<T>(IBasicProperties basicProperties, T objectToSerialize)
    {
        _ = Guard.Argument(basicProperties).NotNull();

        var returnValue = SerializeInternal(basicProperties, objectToSerialize); ;

        return returnValue;
    }
}
