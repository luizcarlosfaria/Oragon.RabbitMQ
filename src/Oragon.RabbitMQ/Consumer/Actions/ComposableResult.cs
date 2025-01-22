// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace Oragon.RabbitMQ.Consumer.Actions;
/// <summary>
/// Represents a composable result that can execute multiple IAmqpResult instances sequentially.
/// </summary>
public class ComposableResult : IAmqpResult
{
    private readonly List<IAmqpResult> results;

    /// <summary>
    /// Initializes a new instance of the <see cref="ComposableResult"/> class with the specified results.
    /// </summary>
    /// <param name="results">The results to be executed.</param>
    internal ComposableResult(params IAmqpResult[] results)
    {
        this.results = results?.ToList() ?? [];
    }

    /// <summary>
    /// Adds a result on top of the list of results.
    /// </summary>
    /// <param name="result"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "<Pending>")]
    public void AddOnTop(IAmqpResult result)
    {
        this.results.Insert(0, result);
    }

    /// <summary>
    /// Adds a result on bottom of the list of results.
    /// </summary>
    /// <param name="result"></param>
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1030:Use events where appropriate", Justification = "<Pending>")]
    public void AddOnBottom(IAmqpResult result)
    {
        this.results.Add(result);
    }


    /// <summary>
    /// Executes all the results sequentially in the provided context.
    /// </summary>
    /// <param name="context">The Amqp context in which to execute the results.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public async Task ExecuteAsync(IAmqpContext context)
    {
        ArgumentNullException.ThrowIfNull(context, nameof(context));

        foreach (IAmqpResult result in this.results)
        {
            await result.ExecuteAsync(context).ConfigureAwait(false);
        }
    }
}
