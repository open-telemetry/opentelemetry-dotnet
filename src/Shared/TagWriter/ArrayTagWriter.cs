// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

internal abstract class ArrayTagWriter<TArrayState>
    where TArrayState : notnull
{
    public abstract TArrayState BeginWriteArray();

    public abstract void WriteNullValue(ref TArrayState state);

    public abstract void WriteIntegralValue(ref TArrayState state, long value);

    public abstract void WriteFloatingPointValue(ref TArrayState state, double value);

    public abstract void WriteBooleanValue(ref TArrayState state, bool value);

    public abstract void WriteStringValue(ref TArrayState state, ReadOnlySpan<char> value);

    public abstract void EndWriteArray(ref TArrayState state);

    public virtual bool TryResize() => false;
}
