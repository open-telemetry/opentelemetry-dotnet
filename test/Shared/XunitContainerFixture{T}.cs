// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using DotNet.Testcontainers.Containers;
using Xunit;

namespace OpenTelemetry.Tests;

public abstract class XunitContainerFixture<T> : ContainerFixture<T>, IAsyncLifetime
    where T : IContainer
{
    Task IAsyncLifetime.DisposeAsync() => this.DisposeAsync().AsTask();
}
