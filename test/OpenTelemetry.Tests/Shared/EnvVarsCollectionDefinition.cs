// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Tests;

/// <summary>
/// Serialization anchor for tests that mutate process-global environment
/// variables. Tests that set or clear env vars should carry
/// <c>[Collection(EnvVarsCollectionDefinition.Name)]</c> so they run
/// sequentially with each other, avoiding cross-class races on shared env
/// var state.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
#pragma warning disable CA1515 // xUnit1027 requires [CollectionDefinition] classes to be public.
public sealed class EnvVarsCollectionDefinition
#pragma warning restore CA1515
{
    public const string Name = "EnvVars";
}
