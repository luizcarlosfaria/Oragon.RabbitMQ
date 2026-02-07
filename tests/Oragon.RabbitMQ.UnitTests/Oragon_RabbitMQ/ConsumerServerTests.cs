using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Oragon.RabbitMQ;
using Oragon.RabbitMQ.Consumer.Dispatch.Attributes;
using Oragon.RabbitMQ.Consumer;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ;

public class ConsumerServerTests
{
    public class TestFakeService
    {
        public void DoAnything(TestDTO testDTO)
        {
            // Do nothing
        }
    }

    public class TestDTO
    {
        public string Name { get; set; }
    }


    [Fact]
    public async Task AddConsumerDescriptor_ShouldAddConsumerDescriptor()
    {

        ConsumerDescriptor consumerDescriptor = new ConsumerDescriptor(
            Mock.Of<IServiceProvider>(),
            "testQueue",
            ([FromServices] TestFakeService svc, [FromBody] TestDTO msg) => svc.DoAnything(msg)
        );


        await using ConsumerServer consumerServer = new ConsumerServer(Mock.Of<ILogger<ConsumerServer>>());
        consumerServer.AddConsumerDescriptor(consumerDescriptor);

        Assert.Single(consumerServer.ConsumerDescriptors);

    }
    

}
