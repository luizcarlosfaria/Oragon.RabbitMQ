// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Oragon.RabbitMQ;

/// <summary>
/// Extensions for Dependency Injection
/// </summary>

[SuppressMessage("Performance", "CA1002", Justification = "Is a handler of this types")]
public static class LinqExtensions
{
    /// <summary>
    /// Reverse a list inline creating a new list with items in reverse order
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="list"></param>
    /// <returns></returns>
    public static List<T> NewReverseList<T>(this List<T> list)
    {
        List<T> returnList = [.. list];
        returnList.Reverse();
        return returnList;
    }

}
