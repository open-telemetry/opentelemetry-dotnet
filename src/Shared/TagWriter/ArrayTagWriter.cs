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

    public virtual void WriteStringValue(ref TArrayState state, string value)
        => this.WriteStringValue(ref state, value.AsSpan());

    public abstract void WriteStringValue(ref TArrayState state, ReadOnlySpan<char> value);

    // Byte arrays and nested arrays/maps are only supported by writers that can embed an
    // arbitrarily-nested value as an array element (currently the JSON-based writers); the
    // default implementations report "not supported" so that TagWriter falls back to writing
    // a string representation of the value instead.
    public virtual bool TryWriteByteArrayValue(ref TArrayState state, ReadOnlySpan<byte> value) => false;

    public virtual bool TryBeginNestedArrayValue(ref TArrayState state) => false;

    public virtual void EndNestedArrayValue(ref TArrayState state)
    {
    }

    public virtual bool TryBeginNestedObjectValue(ref TArrayState state) => false;

    public virtual void WriteNestedObjectPropertyName(ref TArrayState state, string name)
    {
    }

    public virtual void EndNestedObjectValue(ref TArrayState state)
    {
    }

    public abstract void EndWriteArray(ref TArrayState state);

    public virtual void AbortWriteArray(ref TArrayState state)
    {
    }

    public virtual bool TryResize(ref TArrayState state) => false;
}
