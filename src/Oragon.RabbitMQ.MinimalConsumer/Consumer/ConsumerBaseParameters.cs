using Dawn;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer;

public class ConsumerBaseParameters
{

    public string QueueName { get; private set; }
    public ConsumerBaseParameters WithQueueName(string queueName)
    {
        QueueName = queueName;
        return this;
    }

    public Action<IServiceProvider, IModel> Configurer { get; private set; }
    public ConsumerBaseParameters WithTopology(Action<IServiceProvider, IModel> configurer)
    {
        Configurer = configurer;
        return this;
    }

    public ushort PrefetchCount { get; private set; }
    public ConsumerBaseParameters WithPrefetchCount(ushort prefetchCount)
    {
        PrefetchCount = prefetchCount;
        return this;
    }

    public Func<IServiceProvider, IConnection> ConnectionFactoryFunc { get; private set; }
    public ConsumerBaseParameters WithConnectionFactoryFunc(Func<IServiceProvider, IConnection> connectionFactoryFunc)
    {
        ConnectionFactoryFunc = connectionFactoryFunc;
        return this;
    }

    public int TestQueueRetryCount { get; private set; }
    public ConsumerBaseParameters WithTestQueueRetryCount(int testQueueRetryCount)
    {
        TestQueueRetryCount = testQueueRetryCount;
        return this;
    }

    public TimeSpan DisplayLoopInConsoleEvery { get; private set; }
    public ConsumerBaseParameters WithDisplayLoopInConsoleEvery(TimeSpan timeToDisplay)
    {
        DisplayLoopInConsoleEvery = timeToDisplay;
        return this;
    }

    public virtual void Validate()
    {
        Guard.Argument(QueueName).NotNull().NotEmpty().NotWhiteSpace();
        Guard.Argument(PrefetchCount).NotZero().NotNegative();
        Guard.Argument(TestQueueRetryCount).NotNegative();
        Guard.Argument(ConnectionFactoryFunc).NotNull();
    }


}
