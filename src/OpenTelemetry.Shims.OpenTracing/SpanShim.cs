// <copyright file="SpanShim.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
    public sealed class SpanShim : global::OpenTracing.ISpan
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
            this.Span = span ?? throw new ArgumentNullException(nameof(span));

            if (!this.Span.Context.IsValid)
            {
                throw new ArgumentException(nameof(this.Span.Context));
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
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            return this.Context.GetBaggageItems().FirstOrDefault(kvp => kvp.Key.Equals(key)).Value;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan Log(DateTimeOffset timestamp, IEnumerable<KeyValuePair<string, object>> fields)
        {
            if (fields is null)
            {
                throw new ArgumentNullException(nameof(fields));
            }

            var payload = ConvertToEventPayload(fields);
            var eventName = payload.Item1;
            var eventAttributes = payload.Item2;

            this.Span.AddEvent(timestamp == DateTimeOffset.MinValue
                ? new Event(eventName, eventAttributes)
                : new Event(eventName, timestamp, eventAttributes));

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
                throw new ArgumentNullException(nameof(@event));
            }

            this.Span.AddEvent(@event);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan Log(DateTimeOffset timestamp, string @event)
        {
            if (@event is null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            this.Span.AddEvent(new Trace.Event(@event, timestamp));
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetBaggageItem(string key, string value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // TODO Revisit once CorrelationContext is finalized
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetOperationName(string operationName)
        {
            if (operationName is null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            this.Span.UpdateName(operationName);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, string value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.Span.SetAttribute(key, value);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, bool value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // Special case the OpenTracing Error Tag
            // see https://opentracing.io/specification/conventions/
            if (global::OpenTracing.Tag.Tags.Error.Key.Equals(key))
            {
                this.Span.Status = value ? Trace.Status.Unknown : Trace.Status.Ok;
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
                throw new ArgumentNullException(nameof(key));
            }

            this.Span.SetAttribute(key, value);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, double value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
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

                if (eventName == null && field.Key.Equals(LogFields.Event) && field.Value is string value)
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
