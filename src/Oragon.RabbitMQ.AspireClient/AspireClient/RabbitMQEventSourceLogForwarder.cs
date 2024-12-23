// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections;
using System.Diagnostics;
using System.Diagnostics.Tracing;
using Microsoft.Extensions.Logging;

namespace Oragon.RabbitMQ.AspireClient;

internal sealed class RabbitMQEventSourceLogForwarder(ILoggerFactory loggerFactory) : IDisposable
{
    private static readonly Func<ErrorEventSourceEvent, Exception, string> s_formatErrorEvent = FormatErrorEvent;
    private static readonly Func<EventSourceEvent, Exception, string> s_formatEvent = FormatEvent;

    private readonly ILogger _logger = loggerFactory.CreateLogger("RabbitMQ.Client");
    private RabbitMQEventSourceListener _listener;

    /// <summary>
    /// Initiates the log forwarding from the RabbitMQ event sources to a provided <see cref="ILoggerFactory"/>, call <see cref="Dispose"/> to stop forwarding.
    /// </summary>
    public void Start()
    {
        this._listener ??= new RabbitMQEventSourceListener(this.LogEvent, EventLevel.Verbose);
    }

    private void LogEvent(EventWrittenEventArgs eventData)
    {
        LogLevel level = MapLevel(eventData.Level);
        var eventId = new EventId(eventData.EventId, eventData.EventName);

        // Special case the Error event so the Exception Details are written correctly
        if (eventData.EventId == 3 &&
            eventData.EventName == "Error" &&
            eventData.PayloadNames?.Count == 2 &&
            eventData.Payload?.Count == 2 &&
            eventData.PayloadNames[0] == "message" &&
            eventData.PayloadNames[1] == "ex")
        {
            this._logger.Log(level, eventId, new ErrorEventSourceEvent(eventData), null, s_formatErrorEvent);
        }
        else
        {
            Debug.Assert(
                (eventData.EventId == 1 && eventData.EventName == "Info") ||
                (eventData.EventId == 2 && eventData.EventName == "Warn"));

            this._logger.Log(level, eventId, new EventSourceEvent(eventData), null, s_formatEvent);
        }
    }

    private static string FormatErrorEvent(ErrorEventSourceEvent eventSourceEvent, Exception ex)
    {
        return eventSourceEvent.EventData.Payload?[0]?.ToString() ?? "<empty>";
    }

    private static string FormatEvent(EventSourceEvent eventSourceEvent, Exception ex)
    {
        return eventSourceEvent.EventData.Payload?[0]?.ToString() ?? "<empty>";
    }

    public void Dispose()
    {
        this._listener?.Dispose();
    }

    private static LogLevel MapLevel(EventLevel level)
    {
        return level switch
        {
            EventLevel.Critical => LogLevel.Critical,
            EventLevel.Error => LogLevel.Error,
            EventLevel.Informational => LogLevel.Information,
            EventLevel.Verbose => LogLevel.Debug,
            EventLevel.Warning => LogLevel.Warning,
            EventLevel.LogAlways => LogLevel.Information,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, null),
        };
    }

    private readonly struct EventSourceEvent : IReadOnlyList<KeyValuePair<string, object>>
    {
        public EventWrittenEventArgs EventData { get; }

        public EventSourceEvent(EventWrittenEventArgs eventData)
        {
            // only Info and Warn events are expected, which always have 'message' as the only payload
            Debug.Assert(eventData.PayloadNames?.Count == 1 && eventData.PayloadNames[0] == "message");

            this.EventData = eventData;
        }

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (var i = 0; i < this.Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public int Count => this.EventData.PayloadNames?.Count ?? 0;

        public KeyValuePair<string, object> this[int index] => new(this.EventData.PayloadNames![index], this.EventData.Payload![index]);
    }

    private readonly struct ErrorEventSourceEvent(EventWrittenEventArgs eventData) : IReadOnlyList<KeyValuePair<string, object>>
    {
        public EventWrittenEventArgs EventData { get; } = eventData;

        public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
        {
            for (var i = 0; i < this.Count; i++)
            {
                yield return this[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public int Count => 5;

        public KeyValuePair<string, object> this[int index]
        {
            get
            {
                Debug.Assert(this.EventData.PayloadNames?.Count == 2 && this.EventData.Payload?.Count == 2);
                Debug.Assert(this.EventData.PayloadNames[0] == "message");
                Debug.Assert(this.EventData.PayloadNames[1] == "ex");

                ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, 5);

                return index switch
                {
                    0 => new(this.EventData.PayloadNames[0], this.EventData.Payload[0]),
                    < 5 => GetExData(this.EventData, index),
                    _ => throw new UnreachableException()
                };

                static KeyValuePair<string, object> GetExData(EventWrittenEventArgs eventData, int index)
                {
                    Debug.Assert(index is >= 1 and <= 4);
                    Debug.Assert(eventData.Payload?.Count == 2);
                    var exData = eventData.Payload[1] as IDictionary<string, object>;
                    Debug.Assert(exData is not null && exData.Count == 4);

                    return index switch
                    {
                        1 => new("exception.type", exData["Type"]),
                        2 => new("exception.message", exData["Message"]),
                        3 => new("exception.stacktrace", exData["StackTrace"]),
                        4 => new("exception.innerexception", exData["InnerException"]),
                        _ => throw new UnreachableException()
                    };
                }
            }
        }
    }

    /// <summary>
    /// Implementation of <see cref="EventListener"/> that listens to events produced by the RabbitMQ.Client library.
    /// </summary>
    private sealed class RabbitMQEventSourceListener : EventListener
    {
        private readonly List<EventSource> _eventSources = [];

        private readonly Action<EventWrittenEventArgs> _log;
        private readonly EventLevel _level;

        public RabbitMQEventSourceListener(Action<EventWrittenEventArgs> log, EventLevel level)
        {
            this._log = log;
            this._level = level;

            foreach (EventSource eventSource in this._eventSources)
            {
                this.OnEventSourceCreated(eventSource);
            }

            this._eventSources.Clear();
        }

        protected sealed override void OnEventSourceCreated(EventSource eventSource)
        {
            base.OnEventSourceCreated(eventSource);

            if (this._log == null)
            {
                this._eventSources.Add(eventSource);
            }

            if (eventSource.Name is "rabbitmq-dotnet-client" or "rabbitmq-client")
            {
                this.EnableEvents(eventSource, this._level);
            }
        }

        protected sealed override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            // Workaround https://github.com/dotnet/corefx/issues/42600
            if (eventData.EventId == -1)
            {
                return;
            }

            // There is a very tight race during the listener creation where EnableEvents was called
            // and the thread producing events not observing the `_log` field assignment
            this._log?.Invoke(eventData);
        }
    }
}
