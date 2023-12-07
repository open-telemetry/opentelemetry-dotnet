// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Context;

namespace OpenTelemetry.Metrics;

internal sealed class PullMetricScope : IDisposable
{
    private static readonly RuntimeContextSlot<bool> Slot = RuntimeContext.RegisterSlot<bool>("otel.pull_metric");

    private readonly bool previousValue;
    private bool disposed;

    internal PullMetricScope(bool value = true)
    {
        this.previousValue = Slot.Get();
        Slot.Set(value);
    }

    internal static bool IsPullAllowed => Slot.Get();

    public static IDisposable Begin(bool value = true)
    {
        return new PullMetricScope(value);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (!this.disposed)
        {
            Slot.Set(this.previousValue);
            this.disposed = true;
        }
    }
}
