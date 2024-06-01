using Dawn;
using RabbitMQ.Client;
using System.Text;

namespace Oragon.RabbitMQ;

/// <summary>
/// Extensiosn for RabbitMQ
/// </summary>
public static class RabbitMQExtensions
{

    /// <summary>
    /// Reimplementing creation of BasicProperties from IChannel
    /// </summary>
    /// <param name="channel"></param>
    /// <returns></returns>
    public static BasicProperties CreateBasicProperties(this IChannel channel)
    {
        return new BasicProperties();
    }

    /// <summary>
    /// Set MessageId
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="messageId"></param>
    /// <returns></returns>
    public static BasicProperties SetMessageId(this BasicProperties basicProperties, string messageId = null)
    {
        ArgumentNullException.ThrowIfNull(basicProperties);
        basicProperties.MessageId = messageId ?? Guid.NewGuid().ToString("D");
        return basicProperties;
    }


    /// <summary>
    /// Set CorrelationId
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="originalBasicProperties"></param>
    /// <returns></returns>
    public static BasicProperties SetCorrelationId(this BasicProperties basicProperties, IReadOnlyBasicProperties originalBasicProperties)
    {
        _ = Guard.Argument(originalBasicProperties).NotNull().NotSame(basicProperties);
        return basicProperties.SetCorrelationId(originalBasicProperties.MessageId);
    }

    /// <summary>
    /// Set CorrelationId
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="correlationId"></param>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public static BasicProperties SetCorrelationId(this BasicProperties basicProperties, string correlationId)
    {
        _ = Guard.Argument(correlationId).NotNull($"'{nameof(correlationId)}' cannot be null or empty.").NotEmpty($"'{nameof(correlationId)}' cannot be null or empty.").NotWhiteSpace($"'{nameof(correlationId)}' cannot be null or empty.");
        basicProperties.CorrelationId = correlationId;
        return basicProperties;
    }

    /// <summary>
    /// Set if message is Persistent (durable) or not
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="durable"></param>
    /// <returns></returns>
    public static BasicProperties SetDurable(this BasicProperties basicProperties, bool durable = true)
    {
        basicProperties.Persistent = durable;
        return basicProperties;
    }

    /// <summary>
    /// Set if message is Transient (memory only) or not
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="transient"></param>
    /// <returns></returns>
    public static BasicProperties SetTransient(this BasicProperties basicProperties, bool transient = true) => basicProperties.SetDurable(!transient);


    /// <summary>
    /// Set ReplyTo
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="replyTo"></param>
    /// <returns></returns>
    public static BasicProperties SetReplyTo(this BasicProperties basicProperties, string replyTo = null)
    {
        if (!string.IsNullOrEmpty(replyTo))
            basicProperties.ReplyTo = replyTo;

        return basicProperties;
    }

    /// <summary>
    /// Set AppId
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="appId"></param>
    /// <returns></returns>
    public static BasicProperties SetAppId(this BasicProperties basicProperties, string appId = null)
    {
        if (!string.IsNullOrEmpty(appId))
            basicProperties.AppId = appId;

        return basicProperties;
    }

    //private static string AsString(this object objectToConvert)
    //{
    //    return objectToConvert != null ? Encoding.UTF8.GetString((byte[])objectToConvert) : null;
    //}

    /// <summary>
    /// Get a string from a dictionary
    /// </summary>
    /// <param name="dic"></param>
    /// <param name="key"></param>
    /// <returns></returns>
    public static string AsString(this IDictionary<string, object> dic, string key)
    {
        var content = dic?[key];
        return content != null ? Encoding.UTF8.GetString((byte[])content) : null;
    }

    //public static List<string> AsStringList(this object objectToConvert)
    //{
    //    ArgumentNullException.ThrowIfNull(objectToConvert);
    //    var routingKeyList = (List<object>)objectToConvert;

    //    var items = routingKeyList.ConvertAll(key => key.AsString());

    //    return items;
    //}

    /// <summary>
    /// Set Exception on BasicProperties
    /// </summary>
    /// <param name="basicProperties"></param>
    /// <param name="exception"></param>
    /// <returns></returns>
    public static BasicProperties SetException(this BasicProperties basicProperties, Exception exception)
    {
        _ = Guard.Argument(exception).NotNull();

        basicProperties.Headers ??= new Dictionary<string, object>();

        var exceptionType = exception.GetType();

        basicProperties.Headers.Add("exception.type", $"{exceptionType.Namespace}.{exceptionType.Name}, {exceptionType.Assembly.FullName}");
        basicProperties.Headers.Add("exception.message", exception.Message);
        basicProperties.Headers.Add("exception.stacktrace", exception.StackTrace);

        return basicProperties;
    }


    //public static bool TryReconstructException(this BasicProperties basicProperties, out AMQPRemoteException remoteException)
    //{
    //    remoteException = default;
    //    if (basicProperties?.Headers?.ContainsKey("exception.type") ?? false)
    //    {
    //        var exceptionTypeString = basicProperties.Headers.AsString("exception.type");
    //        var exceptionMessage = basicProperties.Headers.AsString("exception.message");
    //        var exceptionStackTrace = basicProperties.Headers.AsString("exception.stacktrace");
    //        var exceptionInstance = (Exception)Activator.CreateInstance(Type.GetType(exceptionTypeString) ?? typeof(Exception), exceptionMessage);
    //        remoteException = new AMQPRemoteException("Remote Consumer report a exception during execution", exceptionStackTrace, exceptionInstance);
    //        return true;
    //    }
    //    return false;
    //}

    /// <summary>
    /// 
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <param name="useAsync"></param>
    /// <returns></returns>
    public static ConnectionFactory DispatchConsumersAsync(this ConnectionFactory connectionFactory, bool useAsync = true)
    {
        _ = Guard.Argument(connectionFactory).NotNull();

        connectionFactory.DispatchConsumersAsync = useAsync;

        return connectionFactory;
    }

    /// <summary>
    /// Convert IConnectionFactory in ConnectionFactory
    /// </summary>
    /// <param name="connectionFactory"></param>
    /// <returns></returns>
    public static ConnectionFactory Unbox(this IConnectionFactory connectionFactory)
        => (ConnectionFactory)connectionFactory;



    //public static List<object> GetDeathHeader(this BasicProperties basicProperties)
    //{
    //    return (List<object>)basicProperties.Headers["x-death"];
    //}

    //public static string GetQueueName(this Dictionary<string, object> xdeath)
    //{
    //    return xdeath.AsString("queue");
    //}

    //public static string GetExchangeName(this Dictionary<string, object> xdeath)
    //{
    //    return xdeath.AsString("exchange");
    //}

    //public static List<string> GetRoutingKeys(this Dictionary<string, object> xdeath)
    //{
    //    return xdeath["routing-keys"].AsStringList();
    //}

    //public static long Count(this Dictionary<string, object> xdeath)
    //{
    //    return (long)xdeath["count"];
    //}

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <param name="condition"></param>
    /// <param name="actionWhenTrue"></param>
    /// <param name="actionWhenFalse"></param>
    /// <returns></returns>
    public static T IfFunction<T>(this T target, Func<T, bool> condition, Func<T, T> actionWhenTrue, Func<T, T> actionWhenFalse = null)
    {
        _ = Guard.Argument(condition, nameof(condition)).NotNull();
        _ = Guard.Argument(actionWhenTrue, nameof(actionWhenTrue)).NotNull();

        if (target != null)
        {
            var conditionResult = condition(target);
            if (conditionResult)
                target = actionWhenTrue(target);
            else if (actionWhenFalse != null)
                target = actionWhenFalse(target);
        }
        return target;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <param name="condition"></param>
    /// <param name="actionWhenTrue"></param>
    /// <param name="actionWhenFalse"></param>
    /// <returns></returns>
    public static T IfAction<T>(this T target, Func<T, bool> condition, Action<T> actionWhenTrue, Action<T> actionWhenFalse = null)
    {
        _ = Guard.Argument(condition, nameof(condition)).NotNull();
        _ = Guard.Argument(actionWhenTrue, nameof(actionWhenTrue)).NotNull();

        if (target != null)
        {
            var conditionResult = condition(target);
            if (conditionResult)
                actionWhenTrue?.Invoke(target);
            else 
                actionWhenFalse?.Invoke(target);

        }
        return target;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <param name="action"></param>
    /// <returns></returns>
    public static T Fluent<T>(this T target, Action action)
        where T : class
    {
        _ = Guard.Argument(target, nameof(target)).NotNull();
        _ = Guard.Argument(action, nameof(action)).NotNull();

        action();

        return target;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="target"></param>
    /// <param name="func"></param>
    /// <returns></returns>
    public static T Fluent<T>(this T target, Func<T> func)
        where T : class
    {
        _ = Guard.Argument(target, nameof(target)).NotNull();
        _ = Guard.Argument(func, nameof(func)).NotNull();

        return func();

    }
}
