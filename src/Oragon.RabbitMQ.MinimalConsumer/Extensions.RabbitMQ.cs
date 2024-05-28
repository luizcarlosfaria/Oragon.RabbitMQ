using Dawn;
using RabbitMQ.Client;
using System.Text;

namespace Oragon.RabbitMQ;

public static partial class RabbitMQExtensions
{
    public static IBasicProperties SetMessageId(this IBasicProperties basicProperties, string messageId = null)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        basicProperties.MessageId = messageId ?? Guid.NewGuid().ToString("D");
        return basicProperties;
    }

    public static IBasicProperties SetCorrelationId(this IBasicProperties basicProperties, IBasicProperties originalBasicProperties)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        ArgumentNullException.ThrowIfNull(originalBasicProperties);

        return basicProperties.SetCorrelationId(originalBasicProperties.MessageId);
    }

    public static IBasicProperties SetCorrelationId(this IBasicProperties basicProperties, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        if (string.IsNullOrEmpty(correlationId)) throw new ArgumentException($"'{nameof(correlationId)}' cannot be null or empty.", nameof(correlationId));

        basicProperties.CorrelationId = correlationId;
        return basicProperties;
    }

    public static IBasicProperties SetDurable(this IBasicProperties basicProperties, bool durable = true)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        basicProperties.Persistent = durable;
        return basicProperties;
    }

    public static IBasicProperties SetReplyTo(this IBasicProperties basicProperties, string replyTo = null)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);

        if (!string.IsNullOrEmpty(replyTo))
            basicProperties.ReplyTo = replyTo;

        return basicProperties;
    }

    public static IBasicProperties SetAppId(this IBasicProperties basicProperties, string appId = null)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);

        if (!string.IsNullOrEmpty(appId))
            basicProperties.AppId = appId;

        return basicProperties;
    }

    private static string AsString(this object objectToConvert)
    {
        return objectToConvert != null ? Encoding.UTF8.GetString((byte[])objectToConvert) : null;
    }

    public static string AsString(this IDictionary<string, object> dic, string key)
    {
        var content = dic?[key];
        return content != null ? Encoding.UTF8.GetString((byte[])content) : null;
    }

    public static List<string> AsStringList(this object objectToConvert)
    {
        ArgumentNullException.ThrowIfNull(objectToConvert);
        var routingKeyList = (List<object>)objectToConvert;

        var items = routingKeyList.ConvertAll(key => key.AsString());

        return items;
    }

    public static IBasicProperties SetException(this IBasicProperties basicProperties, Exception exception)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        ArgumentNullException.ThrowIfNull(exception);

        if (basicProperties.Headers == null) basicProperties.Headers = new Dictionary<string, object>();

        var exceptionType = exception.GetType();

        basicProperties.Headers.Add("exception.type", $"{exceptionType.Namespace}.{exceptionType.Name}, {exceptionType.Assembly.FullName}");
        basicProperties.Headers.Add("exception.message", exception.Message);
        basicProperties.Headers.Add("exception.stacktrace", exception.StackTrace);

        return basicProperties;
    }


    public static bool TryReconstructException(this IBasicProperties basicProperties, out AMQPRemoteException remoteException)
    {
        remoteException = default;
        if (basicProperties?.Headers?.ContainsKey("exception.type") ?? false)
        {
            var exceptionTypeString = basicProperties.Headers.AsString("exception.type");
            var exceptionMessage = basicProperties.Headers.AsString("exception.message");
            var exceptionStackTrace = basicProperties.Headers.AsString("exception.stacktrace");
            var exceptionInstance = (Exception)Activator.CreateInstance(Type.GetType(exceptionTypeString) ?? typeof(Exception), exceptionMessage);
            remoteException = new AMQPRemoteException("Remote consumer report a exception during execution", exceptionStackTrace, exceptionInstance);
            return true;
        }
        return false;
    }

    public static ConnectionFactory DispatchConsumersAsync(this ConnectionFactory connectionFactory, bool useAsync = true)
    {
        connectionFactory.DispatchConsumersAsync = useAsync;
        return connectionFactory;
    }

    public static ConnectionFactory Unbox(this IConnectionFactory connectionFactory) => (ConnectionFactory)connectionFactory;



    public static List<object> GetDeathHeader(this IBasicProperties basicProperties)
    {
        return (List<object>)basicProperties.Headers["x-death"];
    }

    public static string GetQueueName(this Dictionary<string, object> xdeath)
    {
        return xdeath.AsString("queue");
    }

    public static string GetExchangeName(this Dictionary<string, object> xdeath)
    {
        return xdeath.AsString("exchange");
    }

    public static List<string> GetRoutingKeys(this Dictionary<string, object> xdeath)
    {
        return xdeath["routing-keys"].AsStringList();
    }

    public static long Count(this Dictionary<string, object> xdeath)
    {
        return (long)xdeath["count"];
    }

    public static T IfFunction<T>(this T target, Func<T, bool> condition, Func<T, T> actionWhenTrue, Func<T, T> actionWhenFalse = null)
    {
        Guard.Argument(condition, nameof(condition)).NotNull();
        Guard.Argument(actionWhenTrue, nameof(actionWhenTrue)).NotNull();

        if (target == null)
            return target;

        var conditionResult = condition(target);

        if (conditionResult)
            target = actionWhenTrue(target);
        else if (actionWhenFalse != null)
            target = actionWhenFalse(target);

        return target;
    }

    public static T IfAction<T>(this T target, Func<T, bool> condition, Action<T> actionWhenTrue, Action<T> actionWhenFalse = null)
    {
        Guard.Argument(condition, nameof(condition)).NotNull();
        Guard.Argument(actionWhenTrue, nameof(actionWhenTrue)).NotNull();

        if (target == null)
            return target;

        var conditionResult = condition(target);

        if (conditionResult)
            actionWhenTrue(target);
        else actionWhenFalse?.Invoke(target);

        return target;
    }


    public static T Fluent<T>(this T target, Action action)
        where T : class
    {
        Guard.Argument(target, nameof(target)).NotNull();
        Guard.Argument(action, nameof(action)).NotNull();

        action();

        return target;
    }

    public static T Fluent<T>(this T target, Func<T> func)
        where T : class
    {
        Guard.Argument(target, nameof(target)).NotNull();
        Guard.Argument(func, nameof(func)).NotNull();

        return func();

    }
}
