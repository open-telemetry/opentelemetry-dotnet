// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// A text map propagator for W3C trace context. See https://w3c.github.io/trace-context/.
/// </summary>
public class TraceContextPropagator : TextMapPropagator
{
    private const string TraceParent = "traceparent";
    private const string TraceState = "tracestate";

    // The following length limits are from Trace Context v1 https://www.w3.org/TR/trace-context-1/#key
    private const int TraceStateKeyMaxLength = 256;
    private const int TraceStateKeyTenantMaxLength = 241;
    private const int TraceStateKeyVendorMaxLength = 14;
    private const int TraceStateValueMaxLength = 256;

    private static readonly int VersionPrefixIdLength = "00-".Length;
    private static readonly int TraceIdLength = "0af7651916cd43dd8448eb211c80319c".Length;
    private static readonly int VersionAndTraceIdLength = "00-0af7651916cd43dd8448eb211c80319c-".Length;
    private static readonly int SpanIdLength = "00f067aa0ba902b7".Length;
    private static readonly int VersionAndTraceIdAndSpanIdLength = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-".Length;
    private static readonly int OptionsLength = "00".Length;
    private static readonly int TraceparentLengthV0 = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-00".Length;

    /// <inheritdoc/>
    public override ISet<string> Fields => new HashSet<string> { TraceState, TraceParent };

    /// <inheritdoc/>
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        if (context.ActivityContext.IsValid())
        {
            // If a valid context has already been extracted, perform a noop.
            return context;
        }

        if (carrier == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(TraceContextPropagator), "null carrier");
            return context;
        }

        if (getter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToExtractActivityContext(nameof(TraceContextPropagator), "null getter");
            return context;
        }

        try
        {
            if (!TryGetSingleValue(getter(carrier, TraceParent), out var traceparent))
            {
                return context;
            }

            var traceparentParsed = TryExtractTraceparent(traceparent, out var traceId, out var spanId, out var traceoptions);

            if (!traceparentParsed)
            {
                return context;
            }

            string? tracestate = null;
            TryExtractTracestate(getter(carrier, TraceState), out var extractedTracestate, out var hasTraceState);
            if (hasTraceState)
            {
                tracestate = extractedTracestate;
            }

            return new PropagationContext(
                new ActivityContext(traceId, spanId, traceoptions, tracestate, isRemote: true),
                context.Baggage);
        }
        catch (Exception ex)
        {
            OpenTelemetryApiEventSource.Log.ActivityContextExtractException(nameof(TraceContextPropagator), ex);
        }

        // in case of exception indicate to upstream that there is no parseable context from the top
        return context;
    }

    /// <inheritdoc/>
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        if (context.ActivityContext.TraceId == default || context.ActivityContext.SpanId == default)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(TraceContextPropagator), "Invalid context");
            return;
        }

        if (carrier == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(TraceContextPropagator), "null carrier");
            return;
        }

        if (setter == null)
        {
            OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext(nameof(TraceContextPropagator), "null setter");
            return;
        }

#if NET
        var traceparent = string.Create(55, context.ActivityContext, WriteTraceParentIntoSpan);
#else
        var traceparent = string.Concat("00-", context.ActivityContext.TraceId.ToHexString(), "-", context.ActivityContext.SpanId.ToHexString());
        traceparent = string.Concat(traceparent, (context.ActivityContext.TraceFlags & ActivityTraceFlags.Recorded) != 0 ? "-01" : "-00");
#endif

        setter(carrier, TraceParent, traceparent);

        var tracestateStr = context.ActivityContext.TraceState;
        if (tracestateStr?.Length > 0)
        {
            var tracestateEntries = new List<KeyValuePair<string, string>>();
            if (TraceStateUtils.AppendTraceState(tracestateStr, tracestateEntries))
            {
                var normalizedTraceState = TraceStateUtils.GetString(tracestateEntries);
                if (normalizedTraceState.Length > 0)
                {
                    setter(carrier, TraceState, normalizedTraceState);
                }
            }
        }
    }

    internal static bool TryExtractTraceparent(string traceparent, out ActivityTraceId traceId, out ActivitySpanId spanId, out ActivityTraceFlags traceOptions)
    {
        // from https://github.com/w3c/distributed-tracing/blob/master/trace_context/HTTP_HEADER_FORMAT.md
        // traceparent: 00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-01

        traceId = default;
        spanId = default;
        traceOptions = default;
        var bestAttempt = false;

        if (string.IsNullOrWhiteSpace(traceparent) || traceparent.Length < TraceparentLengthV0)
        {
            return false;
        }

        // if version does not end with delimiter
        if (traceparent[VersionPrefixIdLength - 1] != '-')
        {
            return false;
        }

        // or version is not a hex (will throw)
        var version0 = HexCharToByte(traceparent[0]);
        var version1 = HexCharToByte(traceparent[1]);

        if (version0 == 0xf && version1 == 0xf)
        {
            return false;
        }

        if (version0 > 0)
        {
            // expected version is 00
            // for higher versions - best attempt parsing of trace id, span id, etc.
            bestAttempt = true;
        }

        if (traceparent[VersionAndTraceIdLength - 1] != '-')
        {
            return false;
        }

        try
        {
            traceId = ActivityTraceId.CreateFromString(traceparent.AsSpan(VersionPrefixIdLength, TraceIdLength));
        }
        catch (ArgumentOutOfRangeException)
        {
            // it's ok to still parse tracestate
            return false;
        }

        if (traceparent[VersionAndTraceIdAndSpanIdLength - 1] != '-')
        {
            return false;
        }

        byte optionsLowByte;
        try
        {
            spanId = ActivitySpanId.CreateFromString(traceparent.AsSpan(VersionAndTraceIdLength, SpanIdLength));
            _ = HexCharToByte(traceparent[VersionAndTraceIdAndSpanIdLength]); // to verify if there is no bad chars on options position
            optionsLowByte = HexCharToByte(traceparent[VersionAndTraceIdAndSpanIdLength + 1]);
        }
        catch (ArgumentOutOfRangeException)
        {
            // it's ok to still parse tracestate
            return false;
        }

        if ((optionsLowByte & 1) == 1)
        {
            traceOptions |= ActivityTraceFlags.Recorded;
        }

        if ((!bestAttempt) && (traceparent.Length != VersionAndTraceIdAndSpanIdLength + OptionsLength))
        {
            return false;
        }

        if (bestAttempt)
        {
            if ((traceparent.Length > TraceparentLengthV0) && (traceparent[TraceparentLengthV0] != '-'))
            {
                return false;
            }
        }

        return true;
    }

    internal static bool TryExtractTracestate(string[]? tracestateCollection, out string tracestateResult)
        => TryExtractTracestate((IEnumerable<string>?)tracestateCollection, out tracestateResult);

    internal static bool TryExtractTracestate(IEnumerable<string>? tracestateCollection, out string tracestateResult)
        => TryExtractTracestate(tracestateCollection, out tracestateResult, out _);

    private static bool TryExtractTracestate(IEnumerable<string>? tracestateCollection, out string tracestateResult, out bool hasTraceState)
    {
        tracestateResult = string.Empty;
        hasTraceState = false;

        if (tracestateCollection == null)
        {
            return true;
        }

        if (tracestateCollection is IList<string> list)
        {
            if (list.Count == 0)
            {
                return true;
            }

            hasTraceState = true;
            if (list.Count == 1)
            {
                return TryExtractSingleTracestate(list[0], out tracestateResult);
            }

            return TryExtractMultipleTracestate(list, out tracestateResult);
        }

        if (tracestateCollection is IReadOnlyList<string> readOnlyList)
        {
            if (readOnlyList.Count == 0)
            {
                return true;
            }

            hasTraceState = true;
            if (readOnlyList.Count == 1)
            {
                return TryExtractSingleTracestate(readOnlyList[0], out tracestateResult);
            }

            return TryExtractMultipleTracestate(readOnlyList, out tracestateResult);
        }

        using var enumerator = tracestateCollection.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return true;
        }

        hasTraceState = true;
        var singleTraceState = enumerator.Current;
        if (!enumerator.MoveNext())
        {
            return TryExtractSingleTracestate(singleTraceState, out tracestateResult);
        }

        return TryExtractMultipleTracestate(EnumerateFrom(singleTraceState, enumerator), out tracestateResult);
    }

    private static IEnumerable<string> EnumerateFrom(string first, IEnumerator<string> enumerator)
    {
        yield return first;

        do
        {
            yield return enumerator.Current;
        }
        while (enumerator.MoveNext());
    }

    private static bool TryExtractMultipleTracestate(IEnumerable<string> tracestateCollection, out string tracestateResult)
    {
        var keySet = new HashSet<string>();
        var result = new StringBuilder();

        foreach (var tracestateEntry in tracestateCollection)
        {
            var tracestate = tracestateEntry.AsSpan();
            var begin = 0;
            while (begin < tracestate.Length)
            {
                ReadOnlySpan<char> listMember;

                var length = tracestate.Slice(begin).IndexOf(',');
                if (length != -1)
                {
                    listMember = tracestate.Slice(begin, length).Trim();
                    begin += length + 1;
                }
                else
                {
                    listMember = tracestate.Slice(begin).Trim();
                    begin = tracestate.Length;
                }

                // https://github.com/w3c/trace-context/blob/master/spec/20-http_request_header_format.md#tracestate-header-field-values
                if (listMember.IsEmpty)
                {
                    // Empty and whitespace - only list members are allowed.
                    // Vendors MUST accept empty tracestate headers but SHOULD avoid sending them.
                    continue;
                }

                if (keySet.Count >= 32)
                {
                    // https://github.com/w3c/trace-context/blob/master/spec/20-http_request_header_format.md#list
                    // test_tracestate_member_count_limit
                    tracestateResult = string.Empty;
                    return false;
                }

                var keyLength = listMember.IndexOf('=');
                if (keyLength == listMember.Length || keyLength == -1)
                {
                    // Missing key or value in tracestate
                    tracestateResult = string.Empty;
                    return false;
                }

                var key = listMember.Slice(0, keyLength);
                if (!ValidateKey(key))
                {
                    // test_tracestate_key_illegal_characters in https://github.com/w3c/trace-context/blob/master/test/test.py
                    // test_tracestate_key_length_limit
                    // test_tracestate_key_illegal_vendor_format
                    tracestateResult = string.Empty;
                    return false;
                }

                var value = listMember.Slice(keyLength + 1);
                if (!ValidateValue(value))
                {
                    // test_tracestate_value_illegal_characters
                    tracestateResult = string.Empty;
                    return false;
                }

                // ValidateKey() call above has ensured the key does not contain upper case letters.
                if (!keySet.Add(key.ToString()))
                {
                    // test_tracestate_duplicated_keys
                    tracestateResult = string.Empty;
                    return false;
                }

                if (result.Length > 0)
                {
                    result.Append(',');
                }

#if NET
                result.Append(listMember);
#else
                result.Append(listMember.ToString());
#endif
            }
        }

        tracestateResult = result.ToString();
        return true;
    }

    private static bool TryExtractSingleTracestate(string tracestate, out string tracestateResult)
    {
        tracestateResult = string.Empty;

        if (tracestate.Length == 0)
        {
            return true;
        }

        var tracestateSpan = tracestate.AsSpan();

        const int Limit = 32;

        Span<int> memberStarts = stackalloc int[Limit];
        Span<int> memberLengths = stackalloc int[Limit];
        Span<int> keyLengths = stackalloc int[Limit];
        Span<int> keyHashes = stackalloc int[Limit];

        var memberCount = 0;
        var totalLength = 0;
        var normalized = false;
        var begin = 0;

        while (begin < tracestateSpan.Length)
        {
            var end = begin;
            while (end < tracestateSpan.Length && tracestateSpan[end] != ',')
            {
                end++;
            }

            var memberStart = begin;
            var memberEnd = end;

            while (memberStart < memberEnd && char.IsWhiteSpace(tracestateSpan[memberStart]))
            {
                memberStart++;
            }

            while (memberEnd > memberStart && char.IsWhiteSpace(tracestateSpan[memberEnd - 1]))
            {
                memberEnd--;
            }

            if (memberStart != begin || memberEnd != end)
            {
                normalized = true;
            }

            var memberLength = memberEnd - memberStart;
            if (memberLength > 0)
            {
                if (memberCount >= Limit)
                {
                    return false;
                }

                var listMember = tracestateSpan.Slice(memberStart, memberLength);
                var keyLength = listMember.IndexOf('=');
                if (keyLength == listMember.Length || keyLength == -1)
                {
                    return false;
                }

                var key = listMember.Slice(0, keyLength);
                if (!ValidateKey(key))
                {
                    return false;
                }

                var value = listMember.Slice(keyLength + 1);
                if (!ValidateValue(value))
                {
                    return false;
                }

                var useHashedDuplicateCheck = keyLength <= Limit;
                var keyHash = 0;
                if (useHashedDuplicateCheck)
                {
                    keyHash = GetKeyHashCode(key);
                    for (var i = 0; i < memberCount; i++)
                    {
                        if (keyHashes[i] != keyHash || keyLengths[i] != keyLength)
                        {
                            continue;
                        }

                        if (key.SequenceEqual(tracestateSpan.Slice(memberStarts[i], keyLength)))
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    for (var i = 0; i < memberCount; i++)
                    {
                        if (keyLengths[i] == keyLength &&
                            key.SequenceEqual(tracestateSpan.Slice(memberStarts[i], keyLength)))
                        {
                            return false;
                        }
                    }
                }

                memberStarts[memberCount] = memberStart;
                memberLengths[memberCount] = memberLength;
                keyLengths[memberCount] = keyLength;
                keyHashes[memberCount] = keyHash;

                memberCount++;
                totalLength += memberLength;
            }
            else
            {
                normalized = true;
            }

            begin = end + 1;
        }

        if (!normalized && memberCount > 0 && totalLength + memberCount - 1 == tracestate.Length)
        {
            tracestateResult = tracestate;
            return true;
        }

        if (memberCount == 0)
        {
            return true;
        }

        var result = new StringBuilder(totalLength + memberCount - 1);
        for (var i = 0; i < memberCount; i++)
        {
            if (i > 0)
            {
                result.Append(',');
            }

#if NET
            result.Append(tracestateSpan.Slice(memberStarts[i], memberLengths[i]));
#else
            result.Append(tracestate.Substring(memberStarts[i], memberLengths[i]));
#endif
        }

        tracestateResult = result.ToString();
        return true;
    }

    private static byte HexCharToByte(char c)
        => c is >= '0' and <= '9'
           ? (byte)(c - '0')
           : c is >= 'a' and <= 'f'
           ? (byte)(c - 'a' + 10)
           : throw new ArgumentOutOfRangeException(nameof(c), c, "Must be within: [0-9] or [a-f]");

    private static int GetKeyHashCode(ReadOnlySpan<char> key)
    {
#if NET
        HashCode hash = default;

        for (var i = 0; i < key.Length; i++)
        {
            hash.Add(key[i]);
        }

        return hash.ToHashCode();
#else
        unchecked
        {
            var hash = (int)2166136261;
            for (var i = 0; i < key.Length; i++)
            {
                hash = (hash ^ key[i]) * 16777619;
            }

            return hash;
        }
#endif
    }

    private static bool TryGetSingleValue(IEnumerable<string>? values, out string value)
    {
        value = string.Empty;

        if (values == null)
        {
            return false;
        }

        if (values is IList<string> list)
        {
            if (list.Count != 1)
            {
                return false;
            }

            value = list[0];
            return true;
        }

        if (values is IReadOnlyList<string> readOnlyList)
        {
            if (readOnlyList.Count != 1)
            {
                return false;
            }

            value = readOnlyList[0];
            return true;
        }

        using var enumerator = values.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return false;
        }

        value = enumerator.Current;
        return !enumerator.MoveNext();
    }

    private static bool ValidateKey(ReadOnlySpan<char> key)
    {
        // This implementation follows Trace Context v1 which has W3C Recommendation.
        // https://www.w3.org/TR/trace-context-1/#key
        // It will be slightly differently from the next version of specification in GitHub repository.

        // There are two format for the key. The length rule applies to both.
        if (key.Length is <= 0 or > TraceStateKeyMaxLength)
        {
            return false;
        }

        // The first format:
        // key = lcalpha 0*255( lcalpha / DIGIT / "_" / "-"/ "*" / "/" )
        // lcalpha = % x61 - 7A; a - z
        // (There is an inconsistency in the expression above and the description in note.
        // Here is following the description in note:
        // "Identifiers MUST begin with a lowercase letter or a digit.")
        if (!IsLowerAlphaDigit(key[0]))
        {
            return false;
        }

        var tenantLength = -1;
        for (var i = 1; i < key.Length; ++i)
        {
            var ch = key[i];
            if (ch == '@')
            {
                tenantLength = i;
                break;
            }

            if (!(IsLowerAlphaDigit(ch)
                || ch == '_'
                || ch == '-'
                || ch == '*'
                || ch == '/'))
            {
                return false;
            }
        }

        if (tenantLength == -1)
        {
            // There is no "@" sign. The key follow the first format.
            return true;
        }

        // The second format:
        // key = (lcalpha / DIGIT) 0 * 240(lcalpha / DIGIT / "_" / "-" / "*" / "/") "@" lcalpha 0 * 13(lcalpha / DIGIT / "_" / "-" / "*" / "/")
        if (tenantLength is 0 or > TraceStateKeyTenantMaxLength)
        {
            return false;
        }

        var vendorLength = key.Length - tenantLength - 1;
        if (vendorLength is 0 or > TraceStateKeyVendorMaxLength)
        {
            return false;
        }

        for (var i = tenantLength + 1; i < key.Length; ++i)
        {
            var ch = key[i];
            if (!(IsLowerAlphaDigit(ch)
                || ch == '_'
                || ch == '-'
                || ch == '*'
                || ch == '/'))
            {
                return false;
            }
        }

        return true;
    }

    private static bool ValidateValue(ReadOnlySpan<char> value)
    {
        // https://github.com/w3c/trace-context/blob/master/spec/20-http_request_header_format.md#value
        // value      = 0*255(chr) nblk-chr
        // nblk - chr = % x21 - 2B / % x2D - 3C / % x3E - 7E
        // chr        = % x20 / nblk - chr
        if (value.Length is <= 0 or > TraceStateValueMaxLength)
        {
            return false;
        }

        for (var i = 0; i < value.Length - 1; ++i)
        {
            var c = value[i];
            if (c is not (>= (char)0x20 and <= (char)0x7E and not (char)0x2C and not (char)0x3D))
            {
                return false;
            }
        }

        var last = value[value.Length - 1];
        return last is >= (char)0x21 and <= (char)0x7E and not (char)0x2C and not (char)0x3D;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLowerAlphaDigit(char c)
        => c is (>= '0' and <= '9') or (>= 'a' and <= 'z');

#if NET
    private static void WriteTraceParentIntoSpan(Span<char> destination, ActivityContext context)
    {
        "00-".CopyTo(destination);
        context.TraceId.ToHexString().CopyTo(destination.Slice(3));
        destination[35] = '-';
        context.SpanId.ToHexString().CopyTo(destination.Slice(36));
        if ((context.TraceFlags & ActivityTraceFlags.Recorded) != 0)
        {
            "-01".CopyTo(destination.Slice(52));
        }
        else
        {
            "-00".CopyTo(destination.Slice(52));
        }
    }
#endif
}
