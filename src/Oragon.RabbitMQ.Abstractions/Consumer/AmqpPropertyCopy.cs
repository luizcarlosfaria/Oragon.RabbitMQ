// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// AMQP property groups that can be copied from an incoming message to a published message.
/// </summary>
[Flags]
public enum AmqpPropertyCopy
{
    /// <summary>No properties are copied.</summary>
    None = 0,

    /// <summary>Copy message id.</summary>
    MessageId = 1,

    /// <summary>Copy content metadata such as ContentType and ContentEncoding.</summary>
    Content = 2,

    /// <summary>Copy application headers. Only <c>x-delivery-count</c> is filtered: it is quorum-queue delivery state, not message data.</summary>
    Headers = 4,

    /// <summary>Copy delivery mode.</summary>
    DeliveryMode = 8,

    /// <summary>Copy priority.</summary>
    Priority = 16,

    /// <summary>Copy reply metadata.</summary>
    Reply = 32,

    /// <summary>Copy expiration.</summary>
    Expiration = 64,

    /// <summary>Copy timestamp.</summary>
    Timestamp = 128,

    /// <summary>Copy correlation id.</summary>
    CorrelationId = 512,

    /// <summary>Copy AMQP message type.</summary>
    Type = 1024,

    /// <summary>
    /// Copy UserId. RabbitMQ validates <c>user-id</c> against the publishing connection's user
    /// (validated-user-id); copying it from another producer fails the publish unless the
    /// connection user matches or has the <c>impersonator</c> tag.
    /// </summary>
    UserId = 2048,

    /// <summary>Copy AppId.</summary>
    AppId = 4096,

    /// <summary>Copy ClusterId.</summary>
    ClusterId = 8192,

    /// <summary>Copy producer metadata: UserId, AppId and ClusterId.</summary>
    Producer = UserId | AppId | ClusterId,

    /// <summary>Copy message identity fields such as MessageId, CorrelationId and Type.</summary>
    MessageIdentity = MessageId | CorrelationId | Type,

    /// <summary>
    /// Default for RequeueToTail: full copy of the original message, except <see cref="UserId"/>
    /// (validated-user-id would fail the publish on a different connection user).
    /// </summary>
    RequeueToTailDefault = MessageIdentity
        | Content
        | Headers
        | DeliveryMode
        | Priority
        | Reply
        | Expiration
        | Timestamp
        | AppId
        | ClusterId,

    /// <summary>Copy all application-visible property groups, including <see cref="UserId"/>.</summary>
    AllApplicationProperties = MessageIdentity
        | Content
        | Headers
        | DeliveryMode
        | Priority
        | Reply
        | Expiration
        | Timestamp
        | Producer,
}
