// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Globalization;
using System.Text;
using RabbitMQ.Client;

namespace Oragon.RabbitMQ.Consumer;

/// <summary>
/// AMQP headers helper methods.
/// </summary>
public static class AmqpHeaders
{
    /// <summary>
    /// RabbitMQ quorum queue delivery count header.
    /// </summary>
    public const string XDeliveryCountHeader = "x-delivery-count";

    /// <summary>
    /// Gets a typed header value.
    /// </summary>
    /// <typeparam name="T">Target type.</typeparam>
    /// <param name="properties">Basic properties.</param>
    /// <param name="key">Header key.</param>
    /// <returns>Converted value or default.</returns>
    public static T Get<T>(IReadOnlyBasicProperties properties, string key)
    {
        return (T)Get(properties, key, typeof(T));
    }

    /// <summary>
    /// Gets a typed header value.
    /// </summary>
    /// <param name="properties">Basic properties.</param>
    /// <param name="key">Header key.</param>
    /// <param name="targetType">Target type.</param>
    /// <returns>Converted value or default.</returns>
    public static object Get(IReadOnlyBasicProperties properties, string key, Type targetType)
    {
        ArgumentNullException.ThrowIfNull(properties);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(targetType);

        IDictionary<string, object> headers = properties.Headers;

        if (headers == null || !headers.TryGetValue(key, out object value))
        {
            return GetDefault(targetType);
        }

        return ConvertValue(value, targetType);
    }

    /// <summary>
    /// Gets the RabbitMQ quorum queue delivery count.
    /// </summary>
    /// <param name="properties">Basic properties.</param>
    /// <returns>Delivery count or null.</returns>
    public static long? GetDeliveryCount(IReadOnlyBasicProperties properties)
    {
        return Get<long?>(properties, XDeliveryCountHeader);
    }

    /// <summary>
    /// Gets the message priority.
    /// </summary>
    /// <param name="properties">Basic properties.</param>
    /// <returns>Priority.</returns>
    public static byte GetPriority(IReadOnlyBasicProperties properties)
    {
        ArgumentNullException.ThrowIfNull(properties);
        return properties.Priority;
    }

    private static object ConvertValue(object value, Type targetType)
    {
        Type nullableUnderlyingType = Nullable.GetUnderlyingType(targetType);
        Type effectiveType = nullableUnderlyingType ?? targetType;

        if (value == null)
        {
            return GetDefault(targetType);
        }

        if (effectiveType.IsInstanceOfType(value))
        {
            return value;
        }

        if (effectiveType == typeof(string) && value is byte[] bytes)
        {
            return Encoding.UTF8.GetString(bytes);
        }

        if (effectiveType == typeof(byte[]) && value is string stringValue)
        {
            return Encoding.UTF8.GetBytes(stringValue);
        }

        if (effectiveType.IsEnum)
        {
            return Enum.ToObject(effectiveType, value);
        }

        return Convert.ChangeType(value, effectiveType, CultureInfo.InvariantCulture);
    }

    private static object GetDefault(Type targetType)
    {
        return targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null
            ? Activator.CreateInstance(targetType)
            : null;
    }
}
