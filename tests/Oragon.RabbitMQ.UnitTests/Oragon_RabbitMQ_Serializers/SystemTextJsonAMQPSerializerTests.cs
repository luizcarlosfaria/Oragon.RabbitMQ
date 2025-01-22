// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Oragon.RabbitMQ.Serialization;
using Oragon.RabbitMQ.TestsExtensions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Oragon.RabbitMQ.UnitTests.Oragon_RabbitMQ_Serializers;

public class SystemTextJsonAmqpSerializerTests
{
    private sealed class Teste
    {
        public required string Name { get; set; }

        public required int? Age { get; set; }
    }

    [Fact]
    public void SerializationTest()
    {
        var targetBasicProperties = new BasicProperties();

        var sourceObject = new Teste() { Name = "Oragon.RabbitMQ", Age = 2 };

        var serializer = new SystemTextJsonAmqpSerializer(null);

        var serializerOutput = serializer.Serialize(targetBasicProperties, sourceObject);

        var reference = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sourceObject));

        Assert.Equal(reference, serializerOutput);

    }

    [Fact]
    public void DeserializationTest()
    {
        var basicProperties = new BasicProperties();

        var sourceObject = new Teste() { Name = "Oragon.RabbitMQ", Age = 2 };

        var reference = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(sourceObject));

        var serializer = new SystemTextJsonAmqpSerializer(null);

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

        var serializer = new SystemTextJsonAmqpSerializer(null);

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

        var serializer = new SystemTextJsonAmqpSerializer(null);

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

        var serializer = new SystemTextJsonAmqpSerializer(null);

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
    [InlineData([@"{ ""Name"": ""Oragon.RabbitMQ"" }", "Oragon.RabbitMQ", 0, typeof(JsonException)])]
    [InlineData([@"{ ""Name"": ""Oragon.RabbitMQ"", ""Age"": 1 }", "Oragon.RabbitMQ", 1, null])]
    [InlineData([@"{ ""name"": ""Oragon.RabbitMQ"", ""age"": 2 }", "Oragon.RabbitMQ", 2, null])]
    [InlineData([@"{ ""name"": ""Oragon.RabbitMQ"", ""Age"": ""3"" }", "Oragon.RabbitMQ", 3, null])]
    public void TheoryOfDeserializationTest(string json, string expectedName, int expectedAge, Type exceptionType)
    {
        var jsonInBytes = Encoding.UTF8.GetBytes(json);

        IAmqpSerializer serializer = new SystemTextJsonAmqpSerializer(new JsonSerializerOptions()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Skip
        });

        var basicProperties = new BasicProperties();
        var args = new BasicDeliverEventArgs(
                    consumerTag: "-",
                    deliveryTag: 1,
                    redelivered: false,
                    exchange: "-",
                    routingKey: "-",
                    properties: basicProperties.ToReadOnly(),
                    body: jsonInBytes);

        Teste targetObject;

        if (exceptionType != null)
        {
            _ = Assert.Throws(exceptionType, () =>
            {
                targetObject = serializer.Deserialize<Teste>(args);
            });
        }
        else
        {
            targetObject = serializer.Deserialize<Teste>(args);

            Assert.Equal(expectedName, targetObject.Name);
            Assert.Equal(expectedAge, targetObject.Age);
        }


    }
}
