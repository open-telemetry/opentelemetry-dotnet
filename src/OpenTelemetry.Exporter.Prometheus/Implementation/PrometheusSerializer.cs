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

namespace OpenTelemetry.Exporter.Prometheus
{
    /// <summary>
    /// Basic PrometheusSerializer which has no OpenTelemetry dependency.
    /// </summary>
    internal static partial class PrometheusSerializer
    {
#pragma warning disable SA1310 // Field name should not contain an underscore
        private const byte ASCII_QUOTATION_MARK = 0x22; // '"'
        private const byte ASCII_FULL_STOP = 0x2E; // '.'
        private const byte ASCII_HYPHEN_MINUS = 0x2D; // '-'
        private const byte ASCII_REVERSE_SOLIDUS = 0x5C; // '\\'
        private const byte ASCII_LINEFEED = 0x0A; // `\n`
#pragma warning restore SA1310 // Field name should not contain an underscore

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteDouble(byte[] buffer, int cursor, double value)
        {
#if NETCOREAPP3_1_OR_GREATER
            if (double.IsFinite(value))
#else
            if (!double.IsInfinity(value) && !double.IsNaN(value))
#endif
            {
#if NETCOREAPP3_1_OR_GREATER
                Span<char> span = stackalloc char[128];

                var result = value.TryFormat(span, out var cchWritten, "G", CultureInfo.InvariantCulture);
                Debug.Assert(result, $"{nameof(result)} should be true.");

                for (int i = 0; i < cchWritten; i++)
                {
                    buffer[cursor++] = unchecked((byte)span[i]);
                }
#else
                cursor = WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif
            }
            else if (double.IsPositiveInfinity(value))
            {
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "+Inf");
            }
            else if (double.IsNegativeInfinity(value))
            {
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "-Inf");
            }
            else
            {
                Debug.Assert(double.IsNaN(value), $"{nameof(value)} should be NaN.");
                cursor = WriteAsciiStringNoEscape(buffer, cursor, "Nan");
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLong(byte[] buffer, int cursor, long value)
        {
#if NETCOREAPP3_1_OR_GREATER
            Span<char> span = stackalloc char[20];

            var result = value.TryFormat(span, out var cchWritten, "G", CultureInfo.InvariantCulture);
            Debug.Assert(result, $"{nameof(result)} should be true.");

            for (int i = 0; i < cchWritten; i++)
            {
                buffer[cursor++] = unchecked((byte)span[i]);
            }
#else
            cursor = WriteAsciiStringNoEscape(buffer, cursor, value.ToString(CultureInfo.InvariantCulture));
#endif

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteAsciiStringNoEscape(byte[] buffer, int cursor, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                buffer[cursor++] = unchecked((byte)value[i]);
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteUnicodeNoEscape(byte[] buffer, int cursor, ushort ordinal)
        {
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
                        cursor = WriteUnicodeNoEscape(buffer, cursor, ordinal);
                        break;
                }
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLabelKey(byte[] buffer, int cursor, string value)
        {
            Debug.Assert(!string.IsNullOrEmpty(value), $"{nameof(value)} should not be null or empty.");

            var ordinal = (ushort)value[0];

            if (ordinal >= (ushort)'0' && ordinal <= (ushort)'9')
            {
                buffer[cursor++] = unchecked((byte)'_');
            }

            for (int i = 0; i < value.Length; i++)
            {
                ordinal = (ushort)value[i];

                if ((ordinal >= (ushort)'A' && ordinal <= (ushort)'Z') ||
                    (ordinal >= (ushort)'a' && ordinal <= (ushort)'z') ||
                    (ordinal >= (ushort)'0' && ordinal <= (ushort)'9'))
                {
                    buffer[cursor++] = unchecked((byte)ordinal);
                }
                else
                {
                    buffer[cursor++] = unchecked((byte)'_');
                }
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLabelValue(byte[] buffer, int cursor, string value)
        {
            Debug.Assert(value != null, $"{nameof(value)} should not be null.");

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
                        cursor = WriteUnicodeNoEscape(buffer, cursor, ordinal);
                        break;
                }
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteLabel(byte[] buffer, int cursor, string labelKey, object labelValue)
        {
            cursor = WriteLabelKey(buffer, cursor, labelKey);
            buffer[cursor++] = unchecked((byte)'=');
            buffer[cursor++] = unchecked((byte)'"');

            // In Prometheus, a label with an empty label value is considered equivalent to a label that does not exist.
            cursor = WriteLabelValue(buffer, cursor, labelValue?.ToString() ?? string.Empty);
            buffer[cursor++] = unchecked((byte)'"');

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteMetricName(byte[] buffer, int cursor, string metricName, string metricUnit = null)
        {
            Debug.Assert(!string.IsNullOrEmpty(metricName), $"{nameof(metricName)} should not be null or empty.");

            for (int i = 0; i < metricName.Length; i++)
            {
                var ordinal = (ushort)metricName[i];
                switch (ordinal)
                {
                    case ASCII_FULL_STOP:
                    case ASCII_HYPHEN_MINUS:
                        buffer[cursor++] = unchecked((byte)'_');
                        break;
                    default:
                        buffer[cursor++] = unchecked((byte)ordinal);
                        break;
                }
            }

            if (!string.IsNullOrEmpty(metricUnit))
            {
                buffer[cursor++] = unchecked((byte)'_');

                for (int i = 0; i < metricUnit.Length; i++)
                {
                    var ordinal = (ushort)metricUnit[i];

                    if ((ordinal >= (ushort)'A' && ordinal <= (ushort)'Z') ||
                        (ordinal >= (ushort)'a' && ordinal <= (ushort)'z') ||
                        (ordinal >= (ushort)'0' && ordinal <= (ushort)'9'))
                    {
                        buffer[cursor++] = unchecked((byte)ordinal);
                    }
                    else
                    {
                        buffer[cursor++] = unchecked((byte)'_');
                    }
                }
            }

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteHelpText(byte[] buffer, int cursor, string metricName, string metricUnit = null, string metricDescription = null)
        {
            cursor = WriteAsciiStringNoEscape(buffer, cursor, "# HELP ");
            cursor = WriteMetricName(buffer, cursor, metricName, metricUnit);

            if (!string.IsNullOrEmpty(metricDescription))
            {
                buffer[cursor++] = unchecked((byte)' ');
                cursor = WriteUnicodeString(buffer, cursor, metricDescription);
            }

            buffer[cursor++] = ASCII_LINEFEED;

            return cursor;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteTypeInfo(byte[] buffer, int cursor, string metricName, string metricUnit, string metricType)
        {
            Debug.Assert(!string.IsNullOrEmpty(metricType), $"{nameof(metricType)} should not be null or empty.");

            cursor = WriteAsciiStringNoEscape(buffer, cursor, "# TYPE ");
            cursor = WriteMetricName(buffer, cursor, metricName, metricUnit);
            buffer[cursor++] = unchecked((byte)' ');
            cursor = WriteAsciiStringNoEscape(buffer, cursor, metricType);

            buffer[cursor++] = ASCII_LINEFEED;

            return cursor;
        }
    }
}
