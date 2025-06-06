// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Oragon.RabbitMQ;

/// <summary>
/// List and LINQ helper extensions.
/// </summary>

[SuppressMessage("Performance", "CA1002", Justification = "Is a handler of this types")]
public static class LinqExtensions
{
    /// <summary>
    /// Creates a new list containing the elements of the source list in reverse order.
    /// </summary>
    /// <remarks>The original list remains unchanged. This method creates a new list and reverses its
    /// elements.</remarks>
    /// <typeparam name="T">The type of elements in the list.</typeparam>
    /// <param name="list">The source list whose elements will be reversed. If <see langword="null"/>, the method returns <see langword="null"/>.</param>
    /// <returns>A new <see cref="List{T}"/> containing the elements of the source list in reverse order, or <see
    /// langword="null"/> if the source list is <see langword="null"/>.</returns>
    public static List<T> NewReverseList<T>(this List<T> list)
    {
        if (list == null) return null;

        List<T> returnList = [.. list];

        returnList.Reverse();

        return returnList;
    }

}
