// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

internal abstract class KvlistTagWriter<TKvlistState>
    where TKvlistState : notnull
{
    public abstract TKvlistState BeginWriteKvlist();

    public abstract void WriteNullValue(ref TKvlistState state, string key);

    public abstract void WriteIntegralValue(ref TKvlistState state, string key, long value);

    public abstract void WriteFloatingPointValue(ref TKvlistState state, string key, double value);

    public abstract void WriteBooleanValue(ref TKvlistState state, string key, bool value);

    public abstract void WriteStringValue(ref TKvlistState state, string key, ReadOnlySpan<char> value);

    public abstract void WriteArrayValue<TArrayState>(ref TKvlistState state, string key, ref TArrayState arrayState)
        where TArrayState : notnull;

    public abstract void WriteKvlistValue(ref TKvlistState state, string key, ref TKvlistState nestedKvlistState);

    public abstract void EndWriteKvlist(ref TKvlistState state);

    public virtual bool TryResize() => false;
}
