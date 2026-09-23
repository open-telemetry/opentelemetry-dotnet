// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

// This class is a temporary shim until https://github.com/open-telemetry/opentelemetry-dotnet/pull/7805 is merged

namespace Xunit;

internal sealed class TestContext
{
    public static TestContext Current { get; } = new();

    public CancellationToken CancellationToken { get; } = CancellationToken.None;
}
