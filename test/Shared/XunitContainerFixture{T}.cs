// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;
#pragma warning disable IDE0005 // Using directive is unnecessary.
using Xunit;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace OpenTelemetry.Tests;

public abstract class XunitContainerFixture<T> : ContainerFixture<T>, IAsyncLifetime
    where T : IContainer
{
    Task IAsyncLifetime.DisposeAsync() => this.DisposeAsync().AsTask();
}
