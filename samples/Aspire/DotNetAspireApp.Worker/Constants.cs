// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace DotNetAspireApp.Worker;

public static class Constants
{
    public static ushort ConsumerDispatchConcurrency = 4;

    public static ushort Prefetch => (ushort)(Environment.ProcessorCount * 8 * 100);
}
