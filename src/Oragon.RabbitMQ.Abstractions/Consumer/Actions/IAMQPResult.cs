using System.Diagnostics.CodeAnalysis;

namespace Oragon.RabbitMQ.Consumer.Actions;


/// <summary>
/// Represents a AMQP result that can be executed by the consumer after a message is processed.
/// </summary>
[SuppressMessage("Sonar", "S100", Justification = "AMQP is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
[SuppressMessage("Sonar", "S101", Justification = "AMQP is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
public interface IAMQPResult
{

    /// <summary>
    /// Executes the result.
    /// </summary>
    /// <param name="channel"></param>
    /// <param name="delivery"></param>
    Task ExecuteAsync(IAmqpContext context);
}
