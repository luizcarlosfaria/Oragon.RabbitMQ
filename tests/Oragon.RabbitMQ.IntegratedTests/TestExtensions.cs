using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using DotNet.Testcontainers.Builders;
using RabbitMQ.Client;
using Testcontainers.RabbitMq;

namespace Oragon.RabbitMQ.IntegratedTests;
public static class TestExtensions
{
    public static RabbitMqContainer BuildRabbitMQ(this RabbitMqBuilder builder)
    {
        var _rabbitMqContainer = new RabbitMqBuilder()
            .WithImage(Constants.RabbitMQContainerImage)
            .WithExposedPort(15672)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilPortIsAvailable(15672, it => it.WithTimeout(TimeSpan.FromSeconds(60)).WithRetries(10).WithInterval(TimeSpan.FromSeconds(5))))
            .Build();

        return _rabbitMqContainer;
    }

}
