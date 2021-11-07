// <copyright file="PrometheusSerializer.cs" company="OpenTelemetry Authors">
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

#if NETCOREAPP3_1_OR_GREATER
using System;
#endif
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Basic PrometheusSerializer which has no OpenTelemetry dependency.
    /// </summary>
    internal static partial class PrometheusSerializer
    {
#pragma warning disable SA1310 // Field name should not contain an underscore
        private const byte ASCII_QUOTATION_MARK = 0x22; // '"'
        private const byte ASCII_REVERSE_SOLIDUS = 0x5C; // '\\'
        private const byte ASCII_LINEFEED = 0x0A; // `\n`
#pragma warning restore SA1310 // Field name should not contain an underscore

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteDouble(byte[] buffer, int cursor, double value) // TODO: handle +Inf and -Inf
        {
#if NETCOREAPP3_1_OR_GREATER
            Span<char> span = stackalloc char[128];

            var result = value.TryFormat(span, out var cchWritten, "G", CultureInfo.InvariantCulture);
            Debug.Assert(result, "result was not true");

            for (int i = 0; i < cchWritten; i++)
            {
                buffer[cursor++] = unchecked((byte)span[i]);
            }
#else
            var repr = value.ToString(CultureInfo.InvariantCulture);
            cursor += Encoding.UTF8.GetBytes(repr, 0, repr.Length, buffer, cursor);
#endif

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLong(byte[] buffer, int cursor, long value)
        {
#if NETCOREAPP3_1_OR_GREATER
            Span<char> span = stackalloc char[20];

            var result = value.TryFormat(span, out var cchWritten, "G", CultureInfo.InvariantCulture);
            Debug.Assert(result, "result was not true");

            for (int i = 0; i < cchWritten; i++)
            {
                buffer[cursor++] = unchecked((byte)span[i]);
            }
#else
            var repr = value.ToString(CultureInfo.InvariantCulture);
            cursor += Encoding.UTF8.GetBytes(repr, 0, repr.Length, buffer, cursor);
#endif

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteUnicodeStringNoEscape(byte[] buffer, int cursor, string value)
        {
            cursor += Encoding.UTF8.GetBytes(value, 0, value.Length, buffer, cursor);

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteUnicodeString(byte[] buffer, int cursor, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                var ordinal = (ushort)value[i];
                switch (ordinal)
                {
                    case ASCII_REVERSE_SOLIDUS:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        break;
                    case ASCII_LINEFEED:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = unchecked((byte)'n');
                        break;
                    default:
                        if (ordinal <= 0x7F)
                        {
                            buffer[cursor++] = unchecked((byte)ordinal);
                        }
                        else if (ordinal <= 0x07FF)
                        {
                            buffer[cursor++] = unchecked((byte)(0b_1100_0000 | (ordinal >> 6)));
                            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
                        }
                        else if (ordinal <= 0xFFFF)
                        {
                            buffer[cursor++] = unchecked((byte)(0b_1110_0000 | (ordinal >> 12)));
                            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | ((ordinal >> 6) & 0b_0011_1111)));
                            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
                        }
                        else
                        {
                            Debug.Assert(ordinal <= 0xFFFF, ".NET string should not go beyond Unicode BMP.");
                        }

                        break;
                }
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLabelValue(byte[] buffer, int cursor, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                var ordinal = (ushort)value[i];
                switch (ordinal)
                {
                    case ASCII_QUOTATION_MARK:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = ASCII_QUOTATION_MARK;
                        break;
                    case ASCII_REVERSE_SOLIDUS:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        break;
                    case ASCII_LINEFEED:
                        buffer[cursor++] = ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = unchecked((byte)'n');
                        break;
                    default:
                        if (ordinal <= 0x7F)
                        {
                            buffer[cursor++] = unchecked((byte)ordinal);
                        }
                        else if (ordinal <= 0x07FF)
                        {
                            buffer[cursor++] = unchecked((byte)(0b_1100_0000 | (ordinal >> 6)));
                            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
                        }
                        else if (ordinal <= 0xFFFF)
                        {
                            buffer[cursor++] = unchecked((byte)(0b_1110_0000 | (ordinal >> 12)));
                            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | ((ordinal >> 6) & 0b_0011_1111)));
                            buffer[cursor++] = unchecked((byte)(0b_1000_0000 | (ordinal & 0b_0011_1111)));
                        }
                        else
                        {
                            Debug.Assert(ordinal <= 0xFFFF, ".NET string should not go beyond Unicode BMP.");
                        }

                        break;
                }
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLabel(byte[] buffer, int cursor, string labelKey, object labelValue)
        {
            cursor = WriteUnicodeStringNoEscape(buffer, cursor, labelKey);
            buffer[cursor++] = unchecked((byte)'=');
            buffer[cursor++] = unchecked((byte)'"');
            cursor = WriteLabelValue(buffer, cursor, labelValue?.ToString() ?? "null");
            buffer[cursor++] = unchecked((byte)'"');

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteHelpText(byte[] buffer, int cursor, string metricName, string metricDescription = null)
        {
            Debug.Assert(metricName != null, $"{nameof(metricName)} was null.");

            cursor = WriteUnicodeStringNoEscape(buffer, cursor, "# HELP ");
            cursor = WriteUnicodeStringNoEscape(buffer, cursor, metricName);

            if (metricDescription != null)
            {
                buffer[cursor++] = unchecked((byte)' ');
                cursor = WriteUnicodeString(buffer, cursor, metricDescription);
            }

            buffer[cursor++] = ASCII_LINEFEED;

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteTypeInfo(byte[] buffer, int cursor, string metricName, string metricType)
        {
            Debug.Assert(metricName != null, $"{nameof(metricName)} was null.");
            Debug.Assert(metricType != null, $"{nameof(metricType)} was null.");

            cursor = WriteUnicodeStringNoEscape(buffer, cursor, "# TYPE ");
            cursor = WriteUnicodeStringNoEscape(buffer, cursor, metricName);
            buffer[cursor++] = unchecked((byte)' ');
            cursor = WriteUnicodeStringNoEscape(buffer, cursor, metricType);

            buffer[cursor++] = ASCII_LINEFEED;

            return cursor;
        }
    }
}
