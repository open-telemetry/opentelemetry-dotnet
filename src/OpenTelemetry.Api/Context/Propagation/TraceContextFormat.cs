// <copyright file="TraceContextFormat.cs" company="OpenTelemetry Authors">
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
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// W3C trace context text wire protocol formatter. See https://github.com/w3c/distributed-tracing/.
    /// </summary>
    public class TraceContextFormat : ITextFormat
    {
        private const string TraceParent = "traceparent";
        private const string TraceState = "tracestate";
        private const string Baggage = "baggage";
        private const int MaxBaggageLength = 1024;

        private static readonly int VersionPrefixIdLength = "00-".Length;
        private static readonly int TraceIdLength = "0af7651916cd43dd8448eb211c80319c".Length;
        private static readonly int VersionAndTraceIdLength = "00-0af7651916cd43dd8448eb211c80319c-".Length;
        private static readonly int SpanIdLength = "00f067aa0ba902b7".Length;
        private static readonly int VersionAndTraceIdAndSpanIdLength = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-".Length;
        private static readonly int OptionsLength = "00".Length;
        private static readonly int TraceparentLengthV0 = "00-0af7651916cd43dd8448eb211c80319c-00f067aa0ba902b7-00".Length;

        /// <inheritdoc/>
        public ISet<string> Fields => new HashSet<string> { TraceState, TraceParent };

        /// <inheritdoc/>
        public bool IsInjected<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("null carrier");
                return false;
            }

            if (getter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractContext("null getter");
                return false;
            }

            try
            {
                var traceparentCollection = getter(carrier, TraceParent);

                // There must be a single traceparent
                return traceparentCollection != null && traceparentCollection.Count() == 1;
            }
            catch (Exception ex)
            {
                OpenTelemetryApiEventSource.Log.ActivityContextExtractException(ex);
            }

            // in case of exception indicate to upstream that there is no parseable context from the top
            return false;
        }

        /// <inheritdoc/>
        public TextFormatContext Extract<T>(TextFormatContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("null carrier");
                return context;
            }

            if (getter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToExtractContext("null getter");
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

                string tracestate = string.Empty;
                var tracestateCollection = getter(carrier, TraceState);
                if (tracestateCollection?.Any() ?? false)
                {
                    TryExtractTracestate(tracestateCollection.ToArray(), out tracestate);
                }

                IEnumerable<KeyValuePair<string, string>> baggage = null;
                var baggageCollection = getter(carrier, Baggage);
                if (baggageCollection?.Any() ?? false)
                {
                    TryExtractTracestateBaggage(baggageCollection.ToArray(), out baggage);
                }

                return new TextFormatContext(
                    new ActivityContext(traceId, spanId, traceoptions, tracestate, isRemote: true),
                    baggage);
            }
            catch (Exception ex)
            {
                OpenTelemetryApiEventSource.Log.ActivityContextExtractException(ex);
            }

            // in case of exception indicate to upstream that there is no parseable context from the top
            return context;
        }

        /// <inheritdoc/>
        public void Inject<T>(Activity activity, T carrier, Action<T, string, string> setter)
        {
            if (activity.TraceId == default || activity.SpanId == default)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("Invalid context");
                return;
            }

            if (carrier == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("null carrier");
                return;
            }

            if (setter == null)
            {
                OpenTelemetryApiEventSource.Log.FailedToInjectActivityContext("null setter");
                return;
            }

            var traceparent = string.Concat("00-", activity.TraceId.ToHexString(), "-", activity.SpanId.ToHexString());
            traceparent = string.Concat(traceparent, (activity.ActivityTraceFlags & ActivityTraceFlags.Recorded) != 0 ? "-01" : "-00");

            setter(carrier, TraceParent, traceparent);

            string tracestateStr = activity.TraceStateString;
            if (tracestateStr?.Length > 0)
            {
                setter(carrier, TraceState, tracestateStr);
            }

            using IEnumerator<KeyValuePair<string, string>> e = activity.Baggage.GetEnumerator();

            if (e.MoveNext())
            {
                StringBuilder baggage = new StringBuilder();
                do
                {
                    KeyValuePair<string, string> item = e.Current;
                    baggage.Append(WebUtility.UrlEncode(item.Key)).Append('=').Append(WebUtility.UrlEncode(item.Value)).Append(',');
                }
                while (e.MoveNext());
                baggage.Remove(baggage.Length - 1, 1);
                setter(carrier, Baggage, baggage.ToString());
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

            try
            {
                spanId = ActivitySpanId.CreateFromString(traceparent.AsSpan().Slice(VersionAndTraceIdLength, SpanIdLength));
            }
            catch (ArgumentOutOfRangeException)
            {
                // it's ok to still parse tracestate
                return false;
            }

            byte options0;
            byte options1;

            try
            {
                options0 = HexCharToByte(traceparent[VersionAndTraceIdAndSpanIdLength]);
                options1 = HexCharToByte(traceparent[VersionAndTraceIdAndSpanIdLength + 1]);
            }
            catch (ArgumentOutOfRangeException)
            {
                // it's ok to still parse tracestate
                return false;
            }

            if ((options1 & 1) == 1)
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

        internal static bool TryExtractTracestate(string[] tracestateCollection, out string tracestateResult)
        {
            tracestateResult = string.Empty;

            if (tracestateCollection != null)
            {
                var result = new StringBuilder();

                // Iterate in reverse order because when call builder set the elements is added in the
                // front of the list.
                for (int i = tracestateCollection.Length - 1; i >= 0; i--)
                {
                    if (string.IsNullOrEmpty(tracestateCollection[i]))
                    {
                        return false;
                    }

                    result.Append(tracestateCollection[i]);
                }

                tracestateResult = result.ToString();
            }

            return true;
        }

        internal static bool TryExtractTracestateBaggage(string[] baggageCollection, out IEnumerable<KeyValuePair<string, string>> baggage)
        {
            int baggageLength = -1;
            Dictionary<string, string> baggageDictionary = null;

            foreach (var item in baggageCollection)
            {
                if (baggageLength >= MaxBaggageLength)
                {
                    break;
                }

                foreach (var pair in item.Split(','))
                {
                    baggageLength += pair.Length + 1; // pair and comma

                    if (baggageLength >= MaxBaggageLength)
                    {
                        break;
                    }

                    if (NameValueHeaderValue.TryParse(pair, out NameValueHeaderValue baggageItem))
                    {
                        if (baggageDictionary == null)
                        {
                            baggageDictionary = new Dictionary<string, string>();
                        }

                        baggageDictionary[baggageItem.Name] = baggageItem.Value;
                    }
                }
            }

            baggage = baggageDictionary;
            return baggageDictionary != null;
        }

        private static byte HexCharToByte(char c)
        {
            if (((c >= '0') && (c <= '9'))
                || ((c >= 'a') && (c <= 'f'))
                || ((c >= 'A') && (c <= 'F')))
            {
                return Convert.ToByte(c);
            }

            throw new ArgumentOutOfRangeException(nameof(c), $"Invalid character: {c}.");
        }
    }

    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/NameValueHeaderValue.cs
    internal class NameValueHeaderValue
    {
        private static readonly HttpHeaderParser<NameValueHeaderValue> SingleValueParser
            = new GenericHeaderParser<NameValueHeaderValue>(false, GetNameValueLength);

        private string name;
        private string value;

        private NameValueHeaderValue()
        {
            // Used by the parser to create a new instance of this type.
        }

        public string Name
        {
            get { return name; }
        }

        public string Value
        {
            get { return value; }
        }

        public static bool TryParse(string input, out NameValueHeaderValue parsedValue)
        {
            var index = 0;
            return SingleValueParser.TryParseValue(input, ref index, out parsedValue);
        }

        internal static int GetValueLength(string input, int startIndex)
        {
            if (startIndex >= input.Length)
            {
                return 0;
            }

            var valueLength = HttpRuleParser.GetTokenLength(input, startIndex);

            if (valueLength == 0)
            {
                // A value can either be a token or a quoted string. Check if it is a quoted string.
                if (HttpRuleParser.GetQuotedStringLength(input, startIndex, out valueLength) != HttpParseResult.Parsed)
                {
                    // We have an invalid value. Reset the name and return.
                    return 0;
                }
            }

            return valueLength;
        }

        private static int GetNameValueLength(string input, int startIndex, out NameValueHeaderValue parsedValue)
        {
            parsedValue = null;

            if (string.IsNullOrEmpty(input) || (startIndex >= input.Length))
            {
                return 0;
            }

            // Parse the name, i.e. <name> in name/value string "<name>=<value>". Caller must remove
            // leading whitespaces.
            var nameLength = HttpRuleParser.GetTokenLength(input, startIndex);

            if (nameLength == 0)
            {
                return 0;
            }

            var name = input.Substring(startIndex, nameLength);
            var current = startIndex + nameLength;
            current = current + HttpRuleParser.GetWhitespaceLength(input, current);

            // Parse the separator between name and value
            if ((current == input.Length) || (input[current] != '='))
            {
                // We only have a name and that's OK. Return.
                parsedValue = new NameValueHeaderValue();
                parsedValue.name = name;
                current = current + HttpRuleParser.GetWhitespaceLength(input, current); // skip whitespaces
                return current - startIndex;
            }

            current++; // skip delimiter.
            current = current + HttpRuleParser.GetWhitespaceLength(input, current);

            // Parse the value, i.e. <value> in name/value string "<name>=<value>"
            int valueLength = GetValueLength(input, current);

            // Value after the '=' may be empty
            // Use parameterless ctor to avoid double-parsing of name and value, i.e. skip public ctor validation.
            parsedValue = new NameValueHeaderValue();
            parsedValue.name = name;
            parsedValue.value = input.Substring(current, valueLength);
            current = current + valueLength;
            current = current + HttpRuleParser.GetWhitespaceLength(input, current); // skip whitespaces
            return current - startIndex;
        }
    }

    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/HttpHeaderParser.cs
    internal abstract class HttpHeaderParser<T>
    {
        private bool supportsMultipleValues;

        protected HttpHeaderParser(bool supportsMultipleValues)
        {
            this.supportsMultipleValues = supportsMultipleValues;
        }

        public bool SupportsMultipleValues
        {
            get { return supportsMultipleValues; }
        }

        // If a parser supports multiple values, a call to ParseValue/TryParseValue should return a value for 'index'
        // pointing to the next non-whitespace character after a delimiter. E.g. if called with a start index of 0
        // for string "value , second_value", then after the call completes, 'index' must point to 's', i.e. the first
        // non-whitespace after the separator ','.
        public abstract bool TryParseValue(string value, ref int index, out T parsedValue);
    }

    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/HttpParseResult.cs
    internal enum HttpParseResult
    {
        /// <summary>
        /// Parsed succesfully.
        /// </summary>
        Parsed,

        /// <summary>
        /// Was not parsed.
        /// </summary>
        NotParsed,

        /// <summary>
        /// Invalid format.
        /// </summary>
        InvalidFormat,
    }

    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/HttpRuleParser.cs
    internal static class HttpRuleParser
    {
        internal const char CR = '\r';
        internal const char LF = '\n';
        internal const char SP = ' ';
        internal const char Tab = '\t';
        internal const int MaxInt64Digits = 19;
        internal const int MaxInt32Digits = 10;

        private const int MaxNestedCount = 5;
        private static readonly bool[] TokenChars = CreateTokenChars();

        internal static bool IsTokenChar(char character)
        {
            // Must be between 'space' (32) and 'DEL' (127)
            if (character > 127)
            {
                return false;
            }

            return TokenChars[character];
        }

        internal static int GetTokenLength(string input, int startIndex)
        {
            if (startIndex >= input.Length)
            {
                return 0;
            }

            var current = startIndex;

            while (current < input.Length)
            {
                if (!IsTokenChar(input[current]))
                {
                    return current - startIndex;
                }

                current++;
            }

            return input.Length - startIndex;
        }

        internal static int GetWhitespaceLength(string input, int startIndex)
        {
            if (startIndex >= input.Length)
            {
                return 0;
            }

            var current = startIndex;

            char c;
            while (current < input.Length)
            {
                c = input[current];

                if ((c == SP) || (c == Tab))
                {
                    current++;
                    continue;
                }

                if (c == CR)
                {
                    // If we have a #13 char, it must be followed by #10 and then at least one SP or HT.
                    if ((current + 2 < input.Length) && (input[current + 1] == LF))
                    {
                        char spaceOrTab = input[current + 2];
                        if ((spaceOrTab == SP) || (spaceOrTab == Tab))
                        {
                            current += 3;
                            continue;
                        }
                    }
                }

                return current - startIndex;
            }

            // All characters between startIndex and the end of the string are LWS characters.
            return input.Length - startIndex;
        }

        internal static HttpParseResult GetQuotedStringLength(string input, int startIndex, out int length)
        {
            var nestedCount = 0;
            return GetExpressionLength(input, startIndex, '"', '"', false, ref nestedCount, out length);
        }

        // quoted-pair = "\" CHAR
        // CHAR = <any US-ASCII character (octets 0 - 127)>
        internal static HttpParseResult GetQuotedPairLength(string input, int startIndex, out int length)
        {
            length = 0;

            if (input[startIndex] != '\\')
            {
                return HttpParseResult.NotParsed;
            }

            // Quoted-char has 2 characters. Check wheter there are 2 chars left ('\' + char)
            // If so, check whether the character is in the range 0-127. If not, it's an invalid value.
            if ((startIndex + 2 > input.Length) || (input[startIndex + 1] > 127))
            {
                return HttpParseResult.InvalidFormat;
            }

            // We don't care what the char next to '\' is.
            length = 2;
            return HttpParseResult.Parsed;
        }

        private static bool[] CreateTokenChars()
        {
            // token = 1*<any CHAR except CTLs or separators>
            // CTL = <any US-ASCII control character (octets 0 - 31) and DEL (127)>
            var tokenChars = new bool[128]; // everything is false

            for (int i = 33; i < 127; i++)
            {
                // skip Space (32) & DEL (127)
                tokenChars[i] = true;
            }

            // remove separators: these are not valid token characters
            tokenChars[(byte)'('] = false;
            tokenChars[(byte)')'] = false;
            tokenChars[(byte)'<'] = false;
            tokenChars[(byte)'>'] = false;
            tokenChars[(byte)'@'] = false;
            tokenChars[(byte)','] = false;
            tokenChars[(byte)';'] = false;
            tokenChars[(byte)':'] = false;
            tokenChars[(byte)'\\'] = false;
            tokenChars[(byte)'"'] = false;
            tokenChars[(byte)'/'] = false;
            tokenChars[(byte)'['] = false;
            tokenChars[(byte)']'] = false;
            tokenChars[(byte)'?'] = false;
            tokenChars[(byte)'='] = false;
            tokenChars[(byte)'{'] = false;
            tokenChars[(byte)'}'] = false;

            return tokenChars;
        }

        // TEXT = <any OCTET except CTLs, but including LWS>
        // LWS = [CRLF] 1*( SP | HT )
        // CTL = <any US-ASCII control character (octets 0 - 31) and DEL (127)>
        //
        // Since we don't really care about the content of a quoted string or comment, we're more tolerant and
        // allow these characters. We only want to find the delimiters ('"' for quoted string and '(', ')' for comment).
        //
        // 'nestedCount': Comments can be nested. We allow a depth of up to 5 nested comments, i.e. something like
        // "(((((comment)))))". If we wouldn't define a limit an attacker could send a comment with hundreds of nested
        // comments, resulting in a stack overflow exception. In addition having more than 1 nested comment (if any)
        // is unusual.
        private static HttpParseResult GetExpressionLength(
            string input,
            int startIndex,
            char openChar,
            char closeChar,
            bool supportsNesting,
            ref int nestedCount,
            out int length)
        {
            length = 0;

            if (input[startIndex] != openChar)
            {
                return HttpParseResult.NotParsed;
            }

            var current = startIndex + 1; // Start parsing with the character next to the first open-char
            while (current < input.Length)
            {
                // Only check whether we have a quoted char, if we have at least 3 characters left to read (i.e.
                // quoted char + closing char). Otherwise the closing char may be considered part of the quoted char.
                var quotedPairLength = 0;
                if ((current + 2 < input.Length) &&
                    (GetQuotedPairLength(input, current, out quotedPairLength) == HttpParseResult.Parsed))
                {
                    // We ignore invalid quoted-pairs. Invalid quoted-pairs may mean that it looked like a quoted pair,
                    // but we actually have a quoted-string: e.g. "\ü" ('\' followed by a char >127 - quoted-pair only
                    // allows ASCII chars after '\'; qdtext allows both '\' and >127 chars).
                    current = current + quotedPairLength;
                    continue;
                }

                // If we support nested expressions and we find an open-char, then parse the nested expressions.
                if (supportsNesting && (input[current] == openChar))
                {
                    nestedCount++;
                    try
                    {
                        // Check if we exceeded the number of nested calls.
                        if (nestedCount > MaxNestedCount)
                        {
                            return HttpParseResult.InvalidFormat;
                        }

                        var nestedLength = 0;
                        HttpParseResult nestedResult = GetExpressionLength(input, current, openChar, closeChar, supportsNesting, ref nestedCount, out nestedLength);

                        switch (nestedResult)
                        {
                            case HttpParseResult.Parsed:
                                current += nestedLength; // add the length of the nested expression and continue.
                                break;

                            case HttpParseResult.NotParsed:
                                break;

                            case HttpParseResult.InvalidFormat:
                                // If the nested expression is invalid, we can't continue, so we fail with invalid format.
                                return HttpParseResult.InvalidFormat;

                            default:
                                break;
                        }
                    }
                    finally
                    {
                        nestedCount--;
                    }
                }

                if (input[current] == closeChar)
                {
                    length = current - startIndex + 1;
                    return HttpParseResult.Parsed;
                }

                current++;
            }

            // We didn't see the final quote, therefore we have an invalid expression string.
            return HttpParseResult.InvalidFormat;
        }
    }

    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/BaseHeaderParser.cs
    internal abstract class BaseHeaderParser<T> : HttpHeaderParser<T>
    {
        protected BaseHeaderParser(bool supportsMultipleValues)
            : base(supportsMultipleValues)
        {
        }

        public sealed override bool TryParseValue(string value, ref int index, out T parsedValue)
        {
            parsedValue = default(T);

            // If multiple values are supported (i.e. list of values), then accept an empty string: The header may
            // be added multiple times to the request/response message. E.g.
            //  Accept: text/xml; q=1
            //  Accept:
            //  Accept: text/plain; q=0.2
            if (string.IsNullOrEmpty(value) || (index == value.Length))
            {
                return SupportsMultipleValues;
            }

            var separatorFound = false;
            var current = HeaderUtilities.GetNextNonEmptyOrWhitespaceIndex(value, index, SupportsMultipleValues, out separatorFound);

            if (separatorFound && !SupportsMultipleValues)
            {
                return false; // leading separators not allowed if we don't support multiple values.
            }

            if (current == value.Length)
            {
                if (SupportsMultipleValues)
                {
                    index = current;
                }

                return SupportsMultipleValues;
            }

            T result;
            var length = GetParsedValueLength(value, current, out result);

            if (length == 0)
            {
                return false;
            }

            current = current + length;
            current = HeaderUtilities.GetNextNonEmptyOrWhitespaceIndex(value, current, SupportsMultipleValues, out separatorFound);

            // If we support multiple values and we've not reached the end of the string, then we must have a separator.
            if ((separatorFound && !SupportsMultipleValues) || (!separatorFound && (current < value.Length)))
            {
                return false;
            }

            index = current;
            parsedValue = result;
            return true;
        }

        protected abstract int GetParsedValueLength(string value, int startIndex, out T parsedValue);
    }

    // Adoptation of code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/GenericHeaderParser.cs
    internal sealed class GenericHeaderParser<T> : BaseHeaderParser<T>
    {
        private GetParsedValueLengthDelegate getParsedValueLength;

        internal GenericHeaderParser(bool supportsMultipleValues, GetParsedValueLengthDelegate getParsedValueLength)
            : base(supportsMultipleValues)
        {
            if (getParsedValueLength == null)
            {
                throw new ArgumentNullException(nameof(getParsedValueLength));
            }

            this.getParsedValueLength = getParsedValueLength;
        }

        internal delegate int GetParsedValueLengthDelegate(string value, int startIndex, out T parsedValue);

        protected override int GetParsedValueLength(string value, int startIndex, out T parsedValue)
        {
            return getParsedValueLength(value, startIndex, out parsedValue);
        }
    }

    // Adoption of the code from https://github.com/aspnet/HttpAbstractions/blob/07d115400e4f8c7a66ba239f230805f03a14ee3d/src/Microsoft.Net.Http.Headers/HeaderUtilities.cs
    internal static class HeaderUtilities
    {
        internal static int GetNextNonEmptyOrWhitespaceIndex(
            string input,
            int startIndex,
            bool skipEmptyValues,
            out bool separatorFound)
        {
            separatorFound = false;
            var current = startIndex + HttpRuleParser.GetWhitespaceLength(input, startIndex);

            if ((current == input.Length) || (input[current] != ','))
            {
                return current;
            }

            // If we have a separator, skip the separator and all following whitespaces. If we support
            // empty values, continue until the current character is neither a separator nor a whitespace.
            separatorFound = true;
            current++; // skip delimiter.
            current = current + HttpRuleParser.GetWhitespaceLength(input, current);

            if (skipEmptyValues)
            {
                while ((current < input.Length) && (input[current] == ','))
                {
                    current++; // skip delimiter.
                    current = current + HttpRuleParser.GetWhitespaceLength(input, current);
                }
            }

            return current;
        }
    }
}
