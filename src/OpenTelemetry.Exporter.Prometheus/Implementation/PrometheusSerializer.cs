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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace OpenTelemetry.Exporter.Prometheus
{
    internal static class PrometheusSerializer
    {
#pragma warning disable SA1310 // Field name should not contain an underscore
        private const byte ASCII_REVERSE_SOLIDUS = 0x5C; // '\\'
        private const byte ASCII_LINEFEED = 0x0A; // `\n`
#pragma warning restore SA1310 // Field name should not contain an underscore

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int WriteUnicodeString(byte[] buffer, int cursor, string value)
        {
            for (int i = 0; i < value.Length; i++)
            {
                var ordinal = (ushort)value[i];
                switch (ordinal)
                {
                    case PrometheusSerializer.ASCII_REVERSE_SOLIDUS:
                        buffer[cursor++] = PrometheusSerializer.ASCII_REVERSE_SOLIDUS;
                        buffer[cursor++] = PrometheusSerializer.ASCII_REVERSE_SOLIDUS;
                        break;
                    case PrometheusSerializer.ASCII_LINEFEED:
                        buffer[cursor++] = PrometheusSerializer.ASCII_REVERSE_SOLIDUS;
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

        public static int WriteHelpText(byte[] buffer, int cursor, string metricName, string metricDescription = null)
        {
            Debug.Assert(metricName != null, $"{nameof(metricName)} was null.");

            buffer[cursor++] = unchecked((byte)'#');
            buffer[cursor++] = unchecked((byte)' ');
            cursor += Encoding.UTF8.GetBytes(metricName, 0, metricName.Length, buffer, cursor);

            if (metricDescription != null)
            {
                buffer[cursor++] = unchecked((byte)' ');
                cursor = WriteUnicodeString(buffer, cursor, metricDescription);
            }

            return cursor;
        }

        public static int WriteType(byte[] buffer, int cursor, string metricName, string metricType)
        {
            Debug.Assert(metricName != null, $"{nameof(metricName)} was null.");
            Debug.Assert(metricType != null, $"{nameof(metricType)} was null.");

            buffer[cursor++] = unchecked((byte)'#');
            buffer[cursor++] = unchecked((byte)' ');
            cursor += Encoding.UTF8.GetBytes(metricName, 0, metricName.Length, buffer, cursor);
            buffer[cursor++] = unchecked((byte)' ');
            cursor += Encoding.UTF8.GetBytes(metricType, 0, metricType.Length, buffer, cursor);

            return cursor;
        }
    }
}
