// <copyright file="SpanShim.cs" company="OpenTelemetry Authors">
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
using System.Linq;
using global::OpenTracing;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing
{
    internal sealed class SpanShim : global::OpenTracing.ISpan
    {
        /// <summary>
        /// The default event name if not specified.
        /// </summary>
        public const string DefaultEventName = "log";

        private static readonly IReadOnlyCollection<Type> OpenTelemetrySupportedAttributeValueTypes = new List<Type>
        {
            typeof(string),
            typeof(bool),
            typeof(byte),
            typeof(short),
            typeof(int),
            typeof(long),
            typeof(float),
            typeof(double),
        };

        private readonly SpanContextShim spanContextShim;

        public SpanShim(TelemetrySpan span)
        {
            this.Span = span ?? throw new ArgumentNullException(nameof(span), "Parameter cannot be null");

            if (!this.Span.Context.IsValid)
            {
                throw new ArgumentException("Passed span's context is not valid", nameof(this.Span.Context));
            }

            this.spanContextShim = new SpanContextShim(this.Span.Context);
        }

        public ISpanContext Context => this.spanContextShim;

        public TelemetrySpan Span { get; private set; }

        /// <inheritdoc/>
        public void Finish()
        {
            this.Span.End();
        }

        /// <inheritdoc/>
        public void Finish(DateTimeOffset finishTimestamp)
        {
            this.Span.End(finishTimestamp);
        }

        /// <inheritdoc/>
        public string GetBaggageItem(string key)
            => Baggage.GetBaggage(key);

        /// <inheritdoc/>
        public global::OpenTracing.ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            if (fields is null)
            {
                throw new ArgumentNullException(nameof(fields), "Parameter cannot be null");
            }

            var payload = ConvertToEventPayload(fields);
            var eventName = payload.Item1;

            var spanAttributes = new SpanAttributes();
            foreach (var field in payload.Item2)
            {
                switch (field.Value)
                {
                    case long value:
                        spanAttributes.Add(field.Key, value);
                        break;
                    case long[] value:
                        spanAttributes.Add(field.Key, value);
                        break;
                    case bool value:
                        spanAttributes.Add(field.Key, value);
                        break;
                    case bool[] value:
                        spanAttributes.Add(field.Key, value);
                        break;
                    case double value:
                        spanAttributes.Add(field.Key, value);
                        break;
                    case double[] value:
                        spanAttributes.Add(field.Key, value);
                        break;
                    case string value:
                        spanAttributes.Add(field.Key, value);
                        break;
                    case string[] value:
                        spanAttributes.Add(field.Key, value);
                        break;

                    default:
                        break;
                }
            }

            if (timestamp == DateTimeOffset.MinValue)
            {
                this.Span.AddEvent(eventName, spanAttributes);
            }
            else
            {
                this.Span.AddEvent(eventName, timestamp, spanAttributes);
            }

            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan Log(IEnumerable<KeyValuePair<string, object>> fields)
        {
            return this.Log(DateTimeOffset.MinValue, fields);
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan Log(string @event)
        {
            if (@event is null)
            {
                throw new ArgumentNullException(nameof(@event), "Parameter cannot be null");
            }

            this.Span.AddEvent(@event);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan Log(DateTimeOffset timestamp, string @event)
        {
            if (@event is null)
            {
                throw new ArgumentNullException(nameof(@event), "Parameter cannot be null");
            }

            this.Span.AddEvent(@event, timestamp);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetBaggageItem(string key, string value)
        {
            Baggage.SetBaggage(key, value);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetOperationName(string operationName)
        {
            if (operationName is null)
            {
                throw new ArgumentNullException(nameof(operationName), "Parameter cannot be null");
            }

            this.Span.UpdateName(operationName);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, string value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key), "Parameter cannot be null");
            }

            this.Span.SetAttribute(key, value);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, bool value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key), "Parameter cannot be null");
            }

            // Special case the OpenTracing Error Tag
            // see https://opentracing.io/specification/conventions/
            if (global::OpenTracing.Tag.Tags.Error.Key.Equals(key, StringComparison.Ordinal))
            {
                this.Span.SetStatus(value ? Trace.Status.Error : Trace.Status.Ok);
            }
            else
            {
                this.Span.SetAttribute(key, value);
            }

            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, int value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key), "Parameter cannot be null");
            }

            this.Span.SetAttribute(key, value);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, double value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key), "Parameter cannot be null");
            }

            this.Span.SetAttribute(key, value);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(global::OpenTracing.Tag.BooleanTag tag, bool value)
        {
            return this.SetTag(tag?.Key, value);
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(global::OpenTracing.Tag.IntOrStringTag tag, string value)
        {
            if (int.TryParse(value, out var result))
            {
                return this.SetTag(tag?.Key, result);
            }

            return this.SetTag(tag?.Key, value);
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(global::OpenTracing.Tag.IntTag tag, int value)
        {
            return this.SetTag(tag?.Key, value);
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(global::OpenTracing.Tag.StringTag tag, string value)
        {
            return this.SetTag(tag?.Key, value);
        }

        /// <summary>
        /// Constructs an OpenTelemetry event payload from an OpenTracing Log key/value map.
        /// </summary>
        /// <param name="fields">The fields.</param>
        /// <returns>A 2-Tuple containing the event name and payload information.</returns>
        private static Tuple<string, IDictionary<string, object>> ConvertToEventPayload(IEnumerable<KeyValuePair<string, object>> fields)
        {
            string eventName = null;
            var attributes = new Dictionary<string, object>();

            foreach (var field in fields)
            {
                // TODO verify null values are NOT allowed.
                if (field.Value == null)
                {
                    continue;
                }

                // Duplicate keys must be ignored even though they appear to be allowed in OpenTracing.
                if (attributes.ContainsKey(field.Key))
                {
                    continue;
                }

                if (eventName == null && field.Key.Equals(LogFields.Event, StringComparison.Ordinal) && field.Value is string value)
                {
                    // This is meant to be the event name
                    eventName = value;

                    // We don't want to add the event name as a separate attribute
                    continue;
                }

                // Supported types are added directly, all other types are converted to strings.
                if (OpenTelemetrySupportedAttributeValueTypes.Contains(field.Value.GetType()))
                {
                    attributes.Add(field.Key, field.Value);
                }
                else
                {
                    // TODO should we completely ignore unsupported types?
                    attributes.Add(field.Key, field.Value.ToString());
                }
            }

            return new Tuple<string, IDictionary<string, object>>(eventName ?? DefaultEventName, attributes);
        }
    }
}
