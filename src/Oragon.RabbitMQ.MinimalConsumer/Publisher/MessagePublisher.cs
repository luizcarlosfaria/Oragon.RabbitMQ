using OpenTelemetry.Context.Propagation;
using OpenTelemetry;
using RabbitMQ.Client;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Serialization;

namespace Oragon.RabbitMQ.Publisher;
public class MessagePublisher(IConnection connection, IAMQPSerializer serializer, ILogger<MessagePublisher> logger)
{
    private static readonly ActivitySource activitySource = new(MessagingTelemetryNames.GetName(nameof(MessagePublisher)));
    private static readonly TextMapPropagator propagator = Propagators.DefaultTextMapPropagator;

    private readonly IAMQPSerializer serializer = serializer;
    private readonly ILogger<MessagePublisher> logger = logger;
    private readonly IConnection connection = connection;

    public void Send<T>(string exchange, string routingKey, T message)
    {
        using Activity publisherActivity = activitySource.StartActivity("MessagePublisher.Send", ActivityKind.Producer) ?? throw new NullReferenceException(nameof(publisherActivity));

        using IModel model = connection.CreateModel();

        var properties = model.CreateBasicProperties().EnsureHeaders().SetDurable(true);

        var contextToInject = GetActivityContext(publisherActivity);

        // Inject the ActivityContext into the message headers to propagate trace context to the receiving service.
        propagator.Inject(new PropagationContext(contextToInject, Baggage.Current), properties, InjectTraceContextIntoBasicProperties);

        var body = serializer.Serialize(basicProperties: properties, objectToSerialize: message);

        model.BasicPublish(exchange, routingKey, properties, body);

        //publisherActivity?.SetEndTime(DateTime.UtcNow);
    }

    private static ActivityContext GetActivityContext(Activity? activity)
    {
        ActivityContext contextToInject = default;
        if (activity != null)
        {
            contextToInject = activity.Context;
        }
        else if (Activity.Current != null)
        {
            contextToInject = Activity.Current.Context;
        }
        return contextToInject;
    }

    private void InjectTraceContextIntoBasicProperties(IBasicProperties props, string key, string value)
    {
        try
        {
            props.Headers ??= new Dictionary<string, object>();

            props.Headers[key] = value;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to inject trace context.");
        }
    }
}
