using GagoAspireApp.Architecture.Messaging;
using Dawn;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System;
using System.Diagnostics;
using System.Diagnostics.Tracing;

namespace GagoAspireApp.Architecture.Messaging.Serialization;

public abstract class AMQPBaseSerializer : IAMQPSerializer
{
    private readonly ActivitySource activitySource;
    private readonly string name;

    public AMQPBaseSerializer(ActivitySource activitySource, string name)
    {
        this.activitySource = activitySource;
        this.name = name;
    }

    protected abstract TResponse DeserializeInternal<TResponse>(IBasicProperties basicProperties, ReadOnlyMemory<byte> body);

    protected abstract byte[] SerializeInternal<T>(IBasicProperties basicProperties, T objectToSerialize);


    public TResponse Deserialize<TResponse>(BasicDeliverEventArgs eventArgs)
    {
        Guard.Argument(eventArgs).NotNull();
        Guard.Argument(eventArgs.BasicProperties).NotNull();

        using Activity receiveActivity = this.activitySource.SafeStartActivity($"{this.name}.Deserialize", ActivityKind.Internal);
        TResponse returnValue = default;
        try
        {
            returnValue = this.DeserializeInternal<TResponse>(eventArgs.BasicProperties, eventArgs.Body);
        }
        catch (Exception ex)
        {
            receiveActivity?.SetStatus(ActivityStatusCode.Error, ex.ToString());
            throw;
        }
        finally
        {
            receiveActivity?.SetEndTime(DateTime.UtcNow);
        }
        return returnValue;
    }

    public byte[] Serialize<T>(IBasicProperties basicProperties, T objectToSerialize)
    {
        Guard.Argument(basicProperties).NotNull();

        using Activity receiveActivity = this.activitySource.SafeStartActivity($"{this.name}.Serialize", ActivityKind.Internal);
        byte[] returnValue = default;
        try
        {
            returnValue = this.SerializeInternal(basicProperties, objectToSerialize);
        }
        catch (Exception ex)
        {
            receiveActivity?.SetStatus(ActivityStatusCode.Error, ex.ToString());
            throw;
        }
        finally
        {
            receiveActivity?.SetEndTime(DateTime.UtcNow);
        }
        return returnValue;
    }
}
