using System.Diagnostics.CodeAnalysis;

namespace Oragon.RabbitMQ.Consumer.Actions;


/// <summary>
/// Represents a Amqp result that can be executed by the consumer after a message is processed.
/// </summary>
[SuppressMessage("Sonar", "S100", Justification = "Amqp is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
[SuppressMessage("Sonar", "S101", Justification = "Amqp is a acronym for Advanced MessageObject Queuing Protocol, so it's a name.")]
public interface IAmqpResult
{

    /// <summary>
    /// Executes the result.
    /// </summary>
    /// <param name="context"></param>
    Task ExecuteAsync(IAmqpContext context);
}
