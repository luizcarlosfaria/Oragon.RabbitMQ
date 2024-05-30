using Dawn;
using System.Linq.Expressions;
using Oragon.RabbitMQ.Serialization;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// <see langword="static"/> factory for <see cref="AsyncQueueConsumerParameters{TService, TRequest, TResponse}"/>.
/// </summary>
/// <typeparam name="TService"></typeparam>
/// <typeparam name="TRequest"></typeparam>
/// <typeparam name="TResponse"></typeparam>
public class AsyncQueueConsumerParameters<TService, TRequest, TResponse> : ConsumerBaseParameters
    where TResponse : Task
    where TRequest : class
{
    /// <summary>
    /// Service Provider
    /// </summary>
    public IServiceProvider ServiceProvider { get; private set; }

    /// <summary>
    /// Set a <see cref="IServiceProvider"/>
    /// </summary>
    /// <param name="serviceProvider"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        return this;
    }

    /// <summary>
    /// Serializer used to serialize and deserialize messages
    /// </summary>
    public IAMQPSerializer Serializer { get; private set; }

    /// <summary>
    /// Set an IAMQPSerializer
    /// </summary>
    /// <param name="serializer"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithSerializer(IAMQPSerializer serializer)
    {
        Serializer = serializer;
        return this;
    }

    /// <summary>
    /// Call Adapter, used to adapt message to service method
    /// </summary>
    public Expression<Func<TService, TRequest, TResponse>> AdapterExpression { get; private set; }

    /// <summary>
    /// Call Adapter, used to adapt message to service method
    /// </summary>
    public string AdapterExpressionText { get; private set; }

    /// <summary>
    /// Adapter Func, used to adapt message to service method
    /// </summary>
    public Func<TService, TRequest, TResponse> AdapterFunc { get; private set; }

    /// <summary>
    /// Set an Adapter Func
    /// </summary>
    /// <param name="adapterExpression"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithAdapter(Expression<Func<TService, TRequest, TResponse>> adapterExpression)
    {
        _ = Guard.Argument(adapterExpression).NotNull();

        AdapterExpression = adapterExpression;
        AdapterFunc = adapterExpression.Compile();
        AdapterExpressionText = adapterExpression.ToString();
        return this;
    }

    /*public AsyncQueueConsumerParameters<TService, TRequest, TMessage> WithEnterpriseApplicationLog(Func<EnterpriseApplicationLogContext, TRequest, TMessage> adapterFunc)
    {
        _ = Guard.Argument(this.AdapterFunc).NotNull();

        Func<TService, TRequest, TMessage> oldAdapterFunc = this.AdapterFunc;

        this.AdapterFunc = async (svc, msg) =>
        {
            using (var logContext = new EnterpriseApplicationLogContext())
            {
                //logContext.SetIdentity<DiscordSyncService>(nameof(DiscordSyncService.SyncAsync));
                logContext.AddArgument("msg", msg);
                return await logContext.ExecuteWithLogAsync(() => oldAdapterFunc(svc, msg));
            }
        };
        return this;
    }*/

    /// <summary>
    /// Dispatch Scope
    /// </summary>
    public DispatchScope DispatchScope { get; private set; }

    /// <summary>
    /// Set a Dispatch Scope
    /// </summary>
    /// <param name="dispatchScope"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchScope(DispatchScope dispatchScope)
    {
        DispatchScope = dispatchScope;
        return this;
    }

    /// <summary>
    /// Requeue On Crash
    /// </summary>
    public bool RequeueOnCrash { get; private set; }

    /// <summary>
    /// Set Requeue On Crash
    /// </summary>
    /// <param name="requeueOnCrash"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithRequeueOnCrash(bool requeueOnCrash = true)
    {
        RequeueOnCrash = requeueOnCrash;
        return this;
    }


    /// <summary>
    /// Set dispatch in root scope
    /// </summary>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInRootScope()
        => WithDispatchScope(DispatchScope.RootScope);

    /// <summary>
    /// Set dispatch in child scope
    /// </summary>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInChildScope()
        => WithDispatchScope(DispatchScope.ChildScope);


    /// <summary>
    /// Validate parameters
    /// </summary>
    public override void Validate()
    {
        base.Validate();

        _ = Guard.Argument(ServiceProvider).NotNull();
        _ = Guard.Argument(Serializer).NotNull();
        _ = Guard.Argument(AdapterFunc).NotNull();
        _ = Guard.Argument(DispatchScope).NotIn(DispatchScope.None);
    }

}
