// <copyright file="PullMetricScope.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

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
