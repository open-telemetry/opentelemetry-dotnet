// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#nullable enable

namespace OpenTelemetry.Internal;

internal abstract class ArrayTagWriter<T>
    where T : notnull
{
    public abstract T BeginWriteArray();

    public abstract void WriteNullTag(T state);

    public abstract void WriteIntegralTag(T state, long value);

    public abstract void WriteFloatingPointTag(T state, double value);

    public abstract void WriteBooleanTag(T state, bool value);

    public abstract void WriteStringTag(T state, string value);

    public abstract void EndWriteArray(T state);
}
