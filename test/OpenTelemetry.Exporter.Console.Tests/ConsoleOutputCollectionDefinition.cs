// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.Console.Tests;

/// <summary>
/// Required for tests that use <see cref="ConsoleOutputCapture"/>, which
/// redirects the process-global <see cref="System.Console.Out"/> stream and
/// <see cref="System.Diagnostics.Trace.Listeners"/>.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class ConsoleOutputCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "ConsoleOutput";
}
