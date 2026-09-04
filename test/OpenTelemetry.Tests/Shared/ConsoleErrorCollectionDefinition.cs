// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that redirect the process-global
/// <see cref="Console.Error"/> stream. Tests that call
/// <see cref="Console.SetError"/> should carry
/// <c>[Collection(ConsoleErrorCollectionDefinition.Name)]</c> so they run
/// sequentially with each other, avoiding cross-class races on the shared
/// stream.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class ConsoleErrorCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "ConsoleError";
}
