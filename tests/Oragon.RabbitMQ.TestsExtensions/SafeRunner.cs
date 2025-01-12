// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using Polly.Retry;
using Polly;

namespace Oragon.RabbitMQ.TestsExtensions;
/// <summary>
/// Provides a method to safely run a task with retry logic.
/// </summary>
public static class SafeRunner
{
    /// <summary>
    /// Safely runs the specified task with retry logic.
    /// </summary>
    /// <typeparam name="TException">The type of exception to handle.</typeparam>
    /// <param name="taskToRun">The task to run.</param>
    /// <param name="predicate">The predicate to determine if the exception should be retried.</param>
    /// <param name="maxRetryAttempts">The maximum number of retry attempts.</param>
    /// <param name="delayInSeconds">The delay between retry attempts in seconds.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static async Task ExecuteWithRetry<TException>(this Func<Task> taskToRun, Func<TException, bool> predicate = null, int? maxRetryAttempts = 4, int? delayInSeconds = 3)
        where TException : Exception
    {
        var predicateBuilder = predicate != null
            ? new PredicateBuilder<object>().Handle(predicate)
            : new PredicateBuilder<object>().Handle<TException>();


        var pipeline = new ResiliencePipelineBuilder()
            .AddRetry(new RetryStrategyOptions()
            {
                ShouldHandle = predicateBuilder,
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,  // Adds a random factor to the delay
                MaxRetryAttempts = maxRetryAttempts ?? 4,
                Delay = TimeSpan.FromSeconds(delayInSeconds ?? 3),
                OnRetry = (info) =>
                {
                    Console.WriteLine($"Retry {info.AttemptNumber}");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        await pipeline.ExecuteAsync(async (ct) => await taskToRun().ConfigureAwait(true))
            .ConfigureAwait(true);
    }


    /// <summary>
    /// Safely runs the specified task with retry logic.
    /// </summary>
    /// <typeparam name="TException">The type of exception to handle.</typeparam>
    /// <param name="testFunc">The task to run.</param>
    /// <param name="predicate">The predicate to determine if the exception should be retried.</param>
    /// <param name="maxRetryAttempts">The maximum number of retry attempts.</param>
    /// <param name="delayInSeconds">The delay between retry attempts in seconds.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    public static void Wait(this Func<bool> testFunc, int? maxRetryAttempts = 4, int? delayInSeconds = 3)
    {
        var pipeline = new ResiliencePipelineBuilder<bool>()
            .AddRetry(new RetryStrategyOptions<bool>()
            {
                ShouldHandle = new PredicateBuilder<bool>().HandleResult((result) => !result),
                BackoffType = DelayBackoffType.Exponential,
                UseJitter = true,  // Adds a random factor to the delay
                MaxRetryAttempts = maxRetryAttempts ?? 4,
                Delay = TimeSpan.FromSeconds(delayInSeconds ?? 3),
                OnRetry = (info) =>
                {
                    Console.WriteLine($"Retry {info.AttemptNumber}");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();

        _ = pipeline.Execute((ct) =>
        {
            bool testResult = testFunc();
            return testResult;
        });

    }
}
