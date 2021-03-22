// <copyright file="EventSourceEventFormatter.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Linq;

namespace OpenTelemetry.Internal
{
    internal static class EventSourceEventFormatter
    {
        private static readonly object[] EmptyPayload = Array.Empty<object>();

        public static string Format(EventWrittenEventArgs eventData)
        {
            var payloadCollection = eventData.Payload.ToArray() ?? EmptyPayload;

            ProcessPayloadArray(payloadCollection);

            if (eventData.Message != null)
            {
                try
                {
                    return string.Format(CultureInfo.InvariantCulture, eventData.Message, payloadCollection);
                }
                catch (FormatException)
                {
                }
            }

            var stringBuilder = StringBuilderPool.Allocate();

            try
            {
                stringBuilder.Append(eventData.EventName);

                if (!string.IsNullOrWhiteSpace(eventData.Message))
                {
                    stringBuilder.AppendLine();
                    stringBuilder.Append(nameof(eventData.Message)).Append(" = ").Append(eventData.Message);
                }

                if (eventData.PayloadNames != null)
                {
                    for (int i = 0; i < eventData.PayloadNames.Count; i++)
                    {
                        stringBuilder.AppendLine();
                        stringBuilder.Append(eventData.PayloadNames[i]).Append(" = ").Append(payloadCollection[i]);
                    }
                }

                return stringBuilder.ToString();
            }
            finally
            {
                StringBuilderPool.Release(stringBuilder);
            }
        }

        private static void ProcessPayloadArray(object[] payloadArray)
        {
            for (int i = 0; i < payloadArray.Length; i++)
            {
                payloadArray[i] = FormatValue(payloadArray[i]);
            }
        }

        private static object FormatValue(object o)
        {
            if (o is byte[] bytes)
            {
                var stringBuilder = StringBuilderPool.Allocate();

                try
                {
                    foreach (byte b in bytes)
                    {
                        stringBuilder.AppendFormat(CultureInfo.InvariantCulture, "{0:X2}", b);
                    }

                    return stringBuilder.ToString();
                }
                finally
                {
                    StringBuilderPool.Release(stringBuilder);
                }
            }

            return o;
        }
    }
}
