// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Linq.Expressions;
using Dawn;
using Microsoft.Extensions.DependencyInjection;
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
        _ = Guard.Argument(serviceProvider).NotNull();

        this.ServiceProvider = serviceProvider;
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
        _ = Guard.Argument(serializer).NotNull();

        this.Serializer = serializer;
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
    /// Requeue On Crash
    /// </summary>
    public bool RequeueOnCrash { get; private set; }

    /// <summary>
    /// KeyOfService on Keyed Services
    /// </summary>
    public string KeyOfService { get; private set; }

    /// <summary>
    /// Validate service is Keyed Service
    /// </summary>
    public bool IsKeyedService => !string.IsNullOrEmpty(this.KeyOfService);

    /// <summary>
    /// Set an Adapter Func
    /// </summary>
    /// <param name="adapterExpression"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithAdapter(Expression<Func<TService, TRequest, TResponse>> adapterExpression)
    {
        _ = Guard.Argument(adapterExpression).NotNull();

        this.AdapterExpression = adapterExpression;
        this.AdapterFunc = adapterExpression.Compile();
        this.AdapterExpressionText = adapterExpression.ToString();
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
        _ = Guard.Argument(dispatchScope).NotIn(DispatchScope.None);

        this.DispatchScope = dispatchScope;

        return this;
    }



    /// <summary>
    /// Set Requeue On Crash
    /// </summary>
    /// <param name="requeueOnCrash"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithRequeueOnCrash(bool requeueOnCrash = true)
    {
        this.RequeueOnCrash = requeueOnCrash;
        return this;
    }


    /// <summary>
    /// Set dispatch in root scope
    /// </summary>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInRootScope()
        => this.WithDispatchScope(DispatchScope.RootScope);

    /// <summary>
    /// Set dispatch in child scope
    /// </summary>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithDispatchInChildScope()
        => this.WithDispatchScope(DispatchScope.ChildScope);


    /// <summary>
    /// KeyOfService to retrieve service Keyed Services
    /// </summary>
    /// <param name="key"></param>
    /// <returns></returns>
    public AsyncQueueConsumerParameters<TService, TRequest, TResponse> WithKeyedService(string key)
    {
        _ = Guard.Argument(key).NotNull().NotEmpty().NotWhiteSpace();
        this.KeyOfService = key;
        return this;
    }


    /// <summary>
    /// Validate parameters
    /// </summary>
    public override void Validate()
    {
        base.Validate();

        _ = Guard.Argument(this.ServiceProvider).NotNull("ServiceProvider can't be null");
        _ = Guard.Argument(this.Serializer).NotNull("Serializer can't be null");
        _ = Guard.Argument(this.AdapterFunc).NotNull("AdapterFunc can't be null");
        _ = Guard.Argument(this.DispatchScope).NotIn(DispatchScope.None);
    }

}
