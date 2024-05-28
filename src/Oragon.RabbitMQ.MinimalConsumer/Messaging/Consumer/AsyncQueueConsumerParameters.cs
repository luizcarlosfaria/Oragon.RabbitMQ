using DotNetAspire.Architecture.Messaging.Serialization;
using Dawn;
using System.Diagnostics;
using System.Linq.Expressions;

namespace DotNetAspire.Architecture.Messaging.Consumer;

public class AsyncQueueConsumerParameters<TService, TRequest, TResponse> : ConsumerBaseParameters
    where TResponse : Task
    where TRequest : class
{
    public IServiceProvider ServiceProvider { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithServiceProvider(IServiceProvider serviceProvider)
    {
        this.ServiceProvider = serviceProvider;
        return this;
    }

    public IAMQPSerializer Serializer { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithSerializer(IAMQPSerializer serializer)
    {
        this.Serializer = serializer;
        return this;
    }

    public Expression<Func<TService, TRequest, TResponse>>? AdapterExpression { get; private set; }
    public string? AdapterExpressionText { get; private set; }
    public Func<TService, TRequest, TResponse> AdapterFunc { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithAdapter(Expression<Func<TService, TRequest, TResponse>> adapterExpression)
    {
        this.AdapterExpression = adapterExpression;
        this.AdapterFunc = adapterExpression.Compile();
        this.AdapterExpressionText = adapterExpression.ToString();
        return this;
    }

    /*public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithEnterpriseApplicationLog(Func<EnterpriseApplicationLogContext, TRequest, TResponse> adapterFunc)
    {
        Guard.Argument(this.AdapterFunc).NotNull();

        Func<TService, TRequest, TResponse> oldAdapterFunc = this.AdapterFunc;

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
        this.DispatchScope = dispatchScope;
        return this;
    }

    public bool RequeueOnCrash { get; private set; }
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithRequeueOnCrash(bool requeueOnCrash = true)
    {
        this.RequeueOnCrash = requeueOnCrash;
        return this;
    }


    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInRootScope()
        => this.WithDispatchScope(DispatchScope.RootScope);

    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInChildScope()
        => this.WithDispatchScope(DispatchScope.ChildScope);


    public override void Validate()
    {
        base.Validate();

        Guard.Argument(this.ServiceProvider).NotNull();
        Guard.Argument(this.Serializer).NotNull();        
        Guard.Argument(this.AdapterFunc).NotNull();
        Guard.Argument(this.DispatchScope).NotIn(DispatchScope.None);
    }

}
