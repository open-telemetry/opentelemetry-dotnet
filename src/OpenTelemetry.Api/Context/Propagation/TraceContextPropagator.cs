// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// A text map propagator for W3C trace context. See https://w3c.github.io/trace-context/.
/// </summary>
public class TraceContextPropagator : TextMapPropagator
{
    private const string TraceParent = "traceparent";
    private const string TraceState = "tracestate";

    private static readonly int VersionPrefixIdLength = "00-".Length;
    private static readonly int TraceIdLength = "0af7651916cd43dd8448eb211c80319c".Length;
    private static readonly int VersionAndTraceIdLength = "00-0af7651916cd43dd8448eb211c80319c-".Length;
    private static readonly int SpanIdLength = "00f067aa0ba902b7".Length;
    private static readonly int VersionAndTraceIdAndSpanIdLength = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-".Length;
    private static readonly int OptionsLength = "00".Length;
    private static readonly int TraceparentLengthV0 = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-00".Length;

    // The following length limits are from Trace Context v1 https://www.w3.org/TR/trace-context-1/#key
    private static readonly int TraceStateKeyMaxLength = 256;
    private static readonly int TraceStateKeyTenantMaxLength = 241;
    private static readonly int TraceStateKeyVendorMaxLength = 14;
    private static readonly int TraceStateValueMaxLength = 256;

    /// <inheritdoc/>
    public override ISet<string> Fields => new HashSet<string> { TraceState, TraceParent };

    /// <inheritdoc/>
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
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
            var traceparentCollection = getter(carrier, TraceParent);

            // There must be a single traceparent
            if (traceparentCollection == null || traceparentCollection.Count() != 1)
            {
                return context;
            }

            var traceparent = traceparentCollection.First();
            var traceparentParsed = TryExtractTraceparent(traceparent, out var traceId, out var spanId, out var traceoptions);

            if (!traceparentParsed)
            {
                return context;
            }

            string tracestate = null;
            var tracestateCollection = getter(carrier, TraceState);
            if (tracestateCollection?.Any() ?? false)
            {
                TryExtractTracestate(tracestateCollection, out tracestate);
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

        string tracestateStr = context.ActivityContext.TraceState;
        if (tracestateStr?.Length > 0)
        {
            setter(carrier, TraceState, tracestateStr);
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
            traceId = ActivityTraceId.CreateFromString(traceparent.AsSpan().Slice(VersionPrefixIdLength, TraceIdLength));
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
            spanId = ActivitySpanId.CreateFromString(traceparent.AsSpan().Slice(VersionAndTraceIdLength, SpanIdLength));
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

    internal static bool TryExtractTracestate(IEnumerable<string> tracestateCollection, out string tracestateResult)
    {
        tracestateResult = string.Empty;

        char[] rentedArray = null;
        Span<char> traceStateBuffer = stackalloc char[128]; // 256B
        Span<char> keyLookupBuffer = stackalloc char[96]; // 192B (3x32 keys)
        int keys = 0;
        int charsWritten = 0;

        try
        {
            foreach (var tracestateItem in tracestateCollection)
            {
                var tracestate = tracestateItem.AsSpan();
                int position = 0;

                while (position < tracestate.Length)
                {
                    int length = tracestate.Slice(position).IndexOf(',');
                    ReadOnlySpan<char> listMember;

                    if (length != -1)
                    {
                        listMember = tracestate.Slice(position, length).Trim();
                        position += length + 1;
                    }
                    else
                    {
                        listMember = tracestate.Slice(position).Trim();
                        position = tracestate.Length;
                    }

                    // https://github.com/w3c/trace-context/blob/master/spec/20-http_request_header_format.md#tracestate-header-field-values
                    if (listMember.IsEmpty)
                    {
                        // Empty and whitespace - only list members are allowed.
                        // Vendors MUST accept empty tracestate headers but SHOULD avoid sending them.
                        continue;
                    }

                    if (keys >= 32)
                    {
                        // https://github.com/w3c/trace-context/blob/master/spec/20-http_request_header_format.md#list
                        // test_tracestate_member_count_limit
                        return false;
                    }

                    int keyLength = listMember.IndexOf('=');
                    if (keyLength == listMember.Length || keyLength == -1)
                    {
                        // Missing key or value in tracestate
                        return false;
                    }

                    var key = listMember.Slice(0, keyLength);
                    if (!ValidateKey(key))
                    {
                        // test_tracestate_key_illegal_characters in https://github.com/w3c/trace-context/blob/master/test/test.py
                        // test_tracestate_key_length_limit
                        // test_tracestate_key_illegal_vendor_format
                        return false;
                    }

                    var value = listMember.Slice(keyLength + 1);
                    if (!ValidateValue(value))
                    {
                        // test_tracestate_value_illegal_characters
                        return false;
                    }

                    // ValidateKey() call above has ensured the key does not contain upper case letters.

                    var duplicationCheckLength = Math.Min(key.Length, 3);

                    if (keys > 0)
                    {
                        // Fast path check of first three chars for potential duplicated keys
                        var potentialMatchingKeyPosition = 1;
                        var found = false;
                        for (int i = 0; i < keys * 3; i += 3)
                        {
                            if (keyLookupBuffer.Slice(i, duplicationCheckLength).SequenceEqual(key.Slice(0, duplicationCheckLength)))
                            {
                                found = true;
                                break;
                            }

                            potentialMatchingKeyPosition++;
                        }

                        // If the fast check has found a possible duplicate, we need to do a full check
                        if (found)
                        {
                            var bufferToCompare = traceStateBuffer.Slice(0, charsWritten);

                            // We know which key is the first possible duplicate, so skip to that key
                            // by slicing to the position after the appropriate comma.
                            for (int i = 1; i < potentialMatchingKeyPosition; i++)
                            {
                                var commaIndex = bufferToCompare.IndexOf(',');

                                if (commaIndex > -1)
                                {
                                    bufferToCompare.Slice(commaIndex);
                                }
                            }

                            int existingIndex = -1;
                            while ((existingIndex = bufferToCompare.IndexOf(key)) > -1)
                            {
                                if ((existingIndex > 0 && bufferToCompare[existingIndex - 1] != ',') || bufferToCompare[existingIndex + key.Length] != '=')
                                {
                                    continue; // this is not a key
                                }

                                return false; // test_tracestate_duplicated_keys
                            }
                        }
                    }

                    // Store up to the first three characters of the key for use in the duplicate lookup fast path
                    var startKeyLookupIndex = keys > 0 ? keys * 3 : 0;
                    key.Slice(0, duplicationCheckLength).CopyTo(keyLookupBuffer.Slice(startKeyLookupIndex));

                    // Check we have capacity to write the key and value
                    var requiredCapacity = charsWritten > 0 ? listMember.Length + 1 : listMember.Length;

                    while (charsWritten + requiredCapacity > traceStateBuffer.Length)
                    {
                        GrowBuffer(ref rentedArray, ref traceStateBuffer);
                    }

                    if (charsWritten > 0)
                    {
                        traceStateBuffer[charsWritten++] = ',';
                    }

                    listMember.CopyTo(traceStateBuffer.Slice(charsWritten));
                    charsWritten += listMember.Length;

                    keys++;
                }
            }

            tracestateResult = traceStateBuffer.Slice(0, charsWritten).ToString();

            return true;
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<char>.Shared.Return(rentedArray);
                rentedArray = null;
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static void GrowBuffer(ref char[] array, ref Span<char> buffer)
        {
            var newBuffer = ArrayPool<char>.Shared.Rent(buffer.Length * 2);

            buffer.CopyTo(newBuffer.AsSpan());

            if (array is not null)
            {
                ArrayPool<char>.Shared.Return(array);
            }

            array = newBuffer;
            buffer = array.AsSpan();
        }
    }

    private static byte HexCharToByte(char c)
    {
        if ((c >= '0') && (c <= '9'))
        {
            return (byte)(c - '0');
        }

        if ((c >= 'a') && (c <= 'f'))
        {
            return (byte)(c - 'a' + 10);
        }

        throw new ArgumentOutOfRangeException(nameof(c), c, "Must be within: [0-9] or [a-f]");
    }

    private static bool ValidateKey(ReadOnlySpan<char> key)
    {
        // This implementation follows Trace Context v1 which has W3C Recommendation.
        // https://www.w3.org/TR/trace-context-1/#key
        // It will be slightly differently from the next version of specification in GitHub repository.

        // There are two format for the key. The length rule applies to both.
        if (key.Length <= 0 || key.Length > TraceStateKeyMaxLength)
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

        int tenantLength = -1;
        for (int i = 1; i < key.Length; ++i)
        {
            char ch = key[i];
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
        if (tenantLength == 0 || tenantLength > TraceStateKeyTenantMaxLength)
        {
            return false;
        }

        int vendorLength = key.Length - tenantLength - 1;
        if (vendorLength == 0 || vendorLength > TraceStateKeyVendorMaxLength)
        {
            return false;
        }

        for (int i = tenantLength + 1; i < key.Length; ++i)
        {
            char ch = key[i];
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
        if (value.Length <= 0 || value.Length > TraceStateValueMaxLength)
        {
            return false;
        }

        for (int i = 0; i < value.Length - 1; ++i)
        {
            char c = value[i];
            if (!(c >= 0x20 && c <= 0x7E && c != 0x2C && c != 0x3D))
            {
                return false;
            }
        }

        char last = value[value.Length - 1];
        return last >= 0x21 && last <= 0x7E && last != 0x2C && last != 0x3D;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsLowerAlphaDigit(char c)
    {
        return (c >= '0' && c <= '9') || (c >= 'a' && c <= 'z');
    }

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
