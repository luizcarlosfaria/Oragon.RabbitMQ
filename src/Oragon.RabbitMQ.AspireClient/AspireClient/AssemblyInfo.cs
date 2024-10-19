// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Aspire;
using Oragon.RabbitMQ.AspireClient;
using RabbitMQ.Client;

[assembly: ConfigurationSchema("Aspire:RabbitMQ:Client", typeof(RabbitMQClientSettings))]
[assembly: ConfigurationSchema("Aspire:RabbitMQ:Client:ConnectionFactory", typeof(ConnectionFactory), exclusionPaths: ["ClientProperties"])]

[assembly: LoggingCategories("RabbitMQ.Client")]
