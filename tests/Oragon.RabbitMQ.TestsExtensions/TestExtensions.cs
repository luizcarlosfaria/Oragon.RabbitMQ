using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.TestsExtensions;
public static class TestExtensions
{
    public static ReadOnlyBasicProperties ToReadOnly(this BasicProperties basicProperties)
    {
        var bytesSpan = new Span<byte>(new byte[int.MaxValue / 4]);

        IAmqpWriteable amqpWriteable = basicProperties;

        _ = amqpWriteable.WriteTo(bytesSpan);

        var readOnlyBytesSpan = bytesSpan.ToReadOnly();

        var returnValue = new ReadOnlyBasicProperties(readOnlyBytesSpan);

        return returnValue;
    }

    public static ReadOnlySpan<T> ToReadOnly<T>(this Span<T> span)
    {
        var returnValue = new ReadOnlySpan<T>(span.ToArray());

        return returnValue;
    }


}
