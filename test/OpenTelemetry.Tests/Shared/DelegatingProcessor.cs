// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

internal sealed class DelegatingProcessor<T> : BaseProcessor<T>
    where T : class
{
    public Func<int, bool> OnForceFlushFunc { get; set; } = (timeout) => true;

    public Func<int, bool> OnShutdownFunc { get; set; } = (timeout) => true;

    protected override bool OnForceFlush(int timeoutMilliseconds) => this.OnForceFlushFunc(timeoutMilliseconds);

    protected override bool OnShutdown(int timeoutMilliseconds) => this.OnShutdownFunc(timeoutMilliseconds);
}
