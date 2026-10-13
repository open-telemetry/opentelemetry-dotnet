// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

/// <summary>
/// Implemented by a <see cref="TagWriter{TTagState, TArrayState}"/> to have
/// <c>IUtf8SpanFormattable</c> tag values (.NET 8+) formatted directly into its
/// own buffer as string tags. Writers that do not implement it write the
/// value's <see cref="object.ToString"/> result instead.
/// </summary>
/// <typeparam name="TTagState">The writer state type.</typeparam>
internal interface IUtf8StringTagWriter<TTagState>
    where TTagState : notnull
{
    /// <summary>
    /// Starts a string tag and provides the space its UTF-8 value is formatted into.
    /// </summary>
    /// <remarks>
    /// If the value does not fit, an <see cref="ArgumentOutOfRangeException"/>
    /// is thrown to signal that the writer is out of space.
    /// </remarks>
    /// <param name="state">The writer state.</param>
    /// <param name="key">The tag key.</param>
    /// <returns>The space the value is formatted into.</returns>
    Span<byte> GetUtf8Buffer(ref TTagState state, string key);

    /// <summary>
    /// Completes a string tag started by <see cref="GetUtf8Buffer"/>.
    /// </summary>
    /// <param name="state">The writer state.</param>
    /// <param name="utf8Value">The formatted value, at the start of the space from <see cref="GetUtf8Buffer"/>.</param>
    void WriteUtf8StringTag(ref TTagState state, ReadOnlySpan<byte> utf8Value);
}
