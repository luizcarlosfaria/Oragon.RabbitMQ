// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Newtonsoft.Json;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.TestsExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Serializers;
public class NewtonsoftAmqpSerializerTests
{
    private sealed class Teste
    {
        public required string Name { get; set; }

        public required int Age { get; set; }
    }

    [Fact]
    public void SerializationTest()
    {
        var targetBasicProperties = new BasicProperties();

        var sourceObject = new Teste() { Name = "Oragon.RabbitMQ", Age = 2 };

        var serializer = new NewtonsoftAmqpSerializer(null);

        var serializerOutput = serializer.Serialize(targetBasicProperties, sourceObject);

        var reference = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(sourceObject));

        Assert.Equal(reference, serializerOutput);

    }

    [Fact]
    public void DeserializationTest()
    {
        var basicProperties = new BasicProperties();

        var sourceObject = new Teste() { Name = "Oragon.RabbitMQ", Age = 2 };

        var reference = Encoding.UTF8.GetBytes(Newtonsoft.Json.JsonConvert.SerializeObject(sourceObject));

        var serializer = new NewtonsoftAmqpSerializer(null);

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
    public void DeserializationNullTest()
    {
        var basicProperties = new BasicProperties();

        byte[] reference = [];

        var serializer = new NewtonsoftAmqpSerializer(null);

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
    public void DeserializationEmptyTest()
    {
        var basicProperties = new BasicProperties();

        var reference = Encoding.UTF8.GetBytes("");

        var serializer = new NewtonsoftAmqpSerializer(null);

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
    public void DeserializationSpaceTest()
    {
        var basicProperties = new BasicProperties();

        var reference = Encoding.UTF8.GetBytes(" ");

        var serializer = new NewtonsoftAmqpSerializer(null);

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

    [Theory]
    [InlineData([@"{ ""Name"": ""Oragon.RabbitMQ"" }", "Oragon.RabbitMQ", 0])]
    [InlineData([@"{ ""Name"": ""Oragon.RabbitMQ"", ""Age"": 1 }", "Oragon.RabbitMQ", 1])]
    [InlineData([@"{ ""name"": ""Oragon.RabbitMQ"", ""age"": 2 }", "Oragon.RabbitMQ", 2])]
    [InlineData([@"{ ""name"": ""Oragon.RabbitMQ"", ""Age"": ""3"" }", "Oragon.RabbitMQ", 3])]
    public void TheoryOfDeserializationTest(string json, string expectedName, int expectedAge)
    {
        byte[] jsonInBytes = Encoding.UTF8.GetBytes(json);

       
        IAmqpSerializer serializer = new NewtonsoftAmqpSerializer(new JsonSerializerSettings()
        {
            NullValueHandling = NullValueHandling.Ignore
        });

        var basicProperties = new BasicProperties();

        var targetObject = serializer.Deserialize<Teste>(new BasicDeliverEventArgs(
        consumerTag: "-",
        deliveryTag: 1,
        redelivered: false,
        exchange: "-",
        routingKey: "-",
        properties: basicProperties.ToReadOnly(),
        body: jsonInBytes
        ));

        Assert.Equal(expectedName, targetObject.Name);
        Assert.Equal(expectedAge, targetObject.Age);

    }
}
