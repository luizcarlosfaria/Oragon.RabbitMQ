// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.TestsExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Serializer_NewtonsoftJson;
public class NewtonsoftAMQPSerializerTests
{
    private class Teste
    {
        public required string Name { get; set; }

        public required int Age { get; set; }
    }

    [Fact]
    public void SerializationTest()
    {
        var activitySource = new ActivitySource("test");

        var targetBasicProperties = new BasicProperties();

        var sourceObject = new Teste() { Name = "Oragon.RabbitMQ", Age = 2 };

        var serializer = new NewtonsoftAMQPSerializer();

        byte[] serializerOutput = serializer.Serialize(targetBasicProperties, sourceObject);

        byte[] reference = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(sourceObject));

        Assert.Equal(reference, serializerOutput);

    }

    [Fact]
    public void DesserializationTest()
    {
        var activitySource = new ActivitySource("test");

        var basicProperties = new BasicProperties();

        var sourceObject = new Teste() { Name = "Oragon.RabbitMQ", Age = 2 };

        byte[] reference = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(sourceObject));

        var serializer = new NewtonsoftAMQPSerializer();

        var targetObject = serializer.Deserialize<Teste>(new BasicDeliverEventArgs(
            consumerTag: "-",
            deliveryTag: 1,
            redelivered: false,
            exchange: "-",
            routingKey: "-",
            properties: basicProperties.ToReadOnly(),
            body: reference)
            );

        Assert.NotNull(targetObject);
        Assert.Equal(sourceObject.Name, targetObject.Name);
        Assert.Equal(sourceObject.Age, targetObject.Age);

    }

    [Fact]
    public void DesserializationNullTest()
    {
        var activitySource = new ActivitySource("test");

        var basicProperties = new BasicProperties();

        byte[] reference = [];

        var serializer = new NewtonsoftAMQPSerializer();

        var targetObject = serializer.Deserialize<Teste>(new BasicDeliverEventArgs(
            consumerTag: "-",
            deliveryTag: 1,
            redelivered: false,
            exchange: "-",
            routingKey: "-",
            properties: basicProperties.ToReadOnly(),
            body: reference)
            );

        Assert.Null(targetObject);

    }

    [Fact]
    public void DesserializationEmptyTest()
    {
        var activitySource = new ActivitySource("test");

        var basicProperties = new BasicProperties();

        byte[] reference = Encoding.UTF8.GetBytes("");

        var serializer = new NewtonsoftAMQPSerializer();

        var targetObject = serializer.Deserialize<Teste>(new BasicDeliverEventArgs(
            consumerTag: "-",
            deliveryTag: 1,
            redelivered: false,
            exchange: "-",
            routingKey: "-",
            properties: basicProperties.ToReadOnly(),
            body: reference)
            );

        Assert.Null(targetObject);

    }


    [Fact]
    public void DesserializationSpaceTest()
    {
        var activitySource = new ActivitySource("test");

        var basicProperties = new BasicProperties();

        byte[] reference = Encoding.UTF8.GetBytes(" ");

        var serializer = new NewtonsoftAMQPSerializer();

        var targetObject = serializer.Deserialize<Teste>(new BasicDeliverEventArgs(
            consumerTag: "-",
            deliveryTag: 1,
            redelivered: false,
            exchange: "-",
            routingKey: "-",
            properties: basicProperties.ToReadOnly(),
            body: reference)
            );

        Assert.Null(targetObject);

    }
}
