// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests;

public class TestProcessor<T> : BaseProcessor<T>
    where T : class
{
    public Action<T> OnStartAction { get; set; } = (T data) => { };

    public Action<T> OnEndAction { get; set; } = (T data) => { };

    public Func<int, bool> OnForceFlushFunc { get; set; } = (timeout) => true;

    public Func<int, bool> OnShutdownFunc { get; set; } = (timeout) => true;

    public bool ShutdownCalled { get; private set; } = false;

    public bool ForceFlushCalled { get; private set; } = false;

    public bool DisposedCalled { get; private set; } = false;

    public override void OnStart(T data)
    {
        this.OnStartAction(data);
    }

    public override void OnEnd(T data)
    {
        this.OnEndAction(data);
    }

    protected override bool OnForceFlush(int timeoutMilliseconds)
    {
        this.ForceFlushCalled = true;

        return this.OnForceFlushFunc(timeoutMilliseconds);
    }

    protected override bool OnShutdown(int timeoutMilliseconds)
    {
        this.ShutdownCalled = true;

        return this.OnShutdownFunc(timeoutMilliseconds);
    }

    protected override void Dispose(bool disposing)
    {
        this.DisposedCalled = true;
    }
}
