// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Tests.Diagnostics;

/// <summary>
/// A thread-safe <see cref="StringWriter"/>: the dispatcher pump writes on a background
/// thread while tests read <see cref="ToString"/> from the test thread.
/// </summary>
internal sealed class SynchronizedStringWriter : StringWriter
{
    private readonly Lock gate = new();

    public override void Write(char value)
    {
        lock (this.gate)
        {
            base.Write(value);
        }
    }

    public override void Write(char[] buffer, int index, int count)
    {
        lock (this.gate)
        {
            base.Write(buffer, index, count);
        }
    }

    public override void Write(string? value)
    {
        lock (this.gate)
        {
            base.Write(value);
        }
    }

    public override string ToString()
    {
        lock (this.gate)
        {
            return base.ToString();
        }
    }
}
