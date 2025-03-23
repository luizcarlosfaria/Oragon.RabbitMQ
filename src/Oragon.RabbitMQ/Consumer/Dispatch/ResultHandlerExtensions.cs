// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Oragon.RabbitMQ.Consumer.ResultHandlers;

namespace Oragon.RabbitMQ.Consumer.Dispatch;
internal static class ResultHandlerExtensions
{
    internal static IResultHandler FindBestResultHandler(this ConsumerDescriptor consumerDescriptor)
    {
        Type amqpResultType = Constants.IAmqpResult;
        Type taskType = Constants.Task;
        Type returnType = consumerDescriptor.Handler.Method.ReturnType;

        var isTask = returnType.IsAssignableTo(taskType);
        if (isTask)
        {
            if (returnType.IsGenericType && returnType.GenericTypeArguments.Length == 1)
            {
                Type taskValueType = returnType.GenericTypeArguments[0];
                if (taskValueType.IsAssignableTo(amqpResultType))
                {
                    return new TaskOfAmqpResultResultHandler(consumerDescriptor, returnType);
                }
            }
            return new TaskResultHandler(consumerDescriptor);
        }
        else
        {
            return returnType == typeof(void)
                ? new VoidResultHandler()
                : new GenericResultHandler();
        }
    }
}
