// Licensed to LuizCarlosFaria, gaGO.io, Mensageria .NET, Cloud Native .NET and ACADEMIA.DEV under one or more agreements.
// The ACADEMIA.DEV licenses this file to you under the MIT license.

namespace DotNetAspireApp.Common.Messages.Commands;

public record DoSomethingCommand(string Text, int Seq, int Max) { }
