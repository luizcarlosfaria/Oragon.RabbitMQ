using Dawn;
using System.Diagnostics;
using System.Linq.Expressions;
using Oragon.RabbitMQ.Serialization;

namespace Oragon.RabbitMQ.Consumer;

public class AsyncQueueConsumerParameters<TService, TRequest, TResponse> : ConsumerBaseParameters
    where TResponse : Task
    where TRequest : class
{
    public IServiceProvider ServiceProvider { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithServiceProvider(IServiceProvider serviceProvider)
    {
        ServiceProvider = serviceProvider;
        return this;
    }

    public IAMQPSerializer Serializer { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithSerializer(IAMQPSerializer serializer)
    {
        Serializer = serializer;
        return this;
    }

    public Expression<Func<TService, TRequest, TResponse>>? AdapterExpression { get; private set; }
    public string? AdapterExpressionText { get; private set; }
    public Func<TService, TRequest, TResponse> AdapterFunc { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithAdapter(Expression<Func<TService, TRequest, TResponse>> adapterExpression)
    {
        AdapterExpression = adapterExpression;
        AdapterFunc = adapterExpression.Compile();
        AdapterExpressionText = adapterExpression.ToString();
        return this;
    }

    /*public AsyncQueueConsumerParameters<TService, TRequest, TMessage> WithEnterpriseApplicationLog(Func<EnterpriseApplicationLogContext, TRequest, TMessage> adapterFunc)
    {
        Guard.Argument(this.AdapterFunc).NotNull();

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

    public DispatchScope DispatchScope { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchScope(DispatchScope dispatchScope)
    {
        DispatchScope = dispatchScope;
        return this;
    }

    public bool RequeueOnCrash { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithRequeueOnCrash(bool requeueOnCrash = true)
    {
        RequeueOnCrash = requeueOnCrash;
        return this;
    }


    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInRootScope()
        => WithDispatchScope(DispatchScope.RootScope);

    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInChildScope()
        => WithDispatchScope(DispatchScope.ChildScope);


    public override void Validate()
    {
        base.Validate();

        Guard.Argument(ServiceProvider).NotNull();
        Guard.Argument(Serializer).NotNull();
        Guard.Argument(AdapterFunc).NotNull();
        Guard.Argument(DispatchScope).NotIn(DispatchScope.None);
    }

}
