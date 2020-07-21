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
using System.Diagnostics;
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

        public SpanShim(TelemetrySpanNew span)
        {
            this.Span = span ?? throw new ArgumentNullException(nameof(span));

            if (!this.Span.Context.IsValid)
            {
                throw new ArgumentException(nameof(this.Span.Context));
            }

            this.activity = span.activity;
            this.spanContextShim = new SpanContextShim(this.Span.Context);
        }

        public ISpanContext Context => this.spanContextShim;

        public TelemetrySpanNew Span { get; private set; }

#pragma warning disable SA1300 // Other variable name will cause confusion, as it is referenced in other classes
        public Activity activity { get; private set; }
#pragma warning restore SA1300 // Other variable name will cause confusion, as it is referenced in other classes

        /// <inheritdoc/>
        public void Finish()
        {
            this.activity.Stop();
        }

        /// <inheritdoc/>
        public void Finish(DateTimeOffset finishTimestamp)
        {
            this.activity.SetEndTime(finishTimestamp.UtcDateTime);
            this.activity.Stop();
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

            this.activity.AddEvent(timestamp == DateTimeOffset.MinValue
                ? new ActivityEvent(eventName, eventAttributes)
                : new ActivityEvent(eventName, timestamp, eventAttributes));

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

            this.activity.AddEvent(new ActivityEvent(@event));
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan Log(DateTimeOffset timestamp, string @event)
        {
            if (@event is null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            this.activity.AddEvent(new ActivityEvent(@event, timestamp));
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetBaggageItem(string key, string value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.activity.AddBaggage(key, value);
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetOperationName(string operationName)
        {
            if (operationName is null)
            {
                throw new ArgumentNullException(nameof(operationName));
            }

            this.activity.DisplayName = operationName;
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, string value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            this.activity.AddTag(key, value);
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
                this.activity.SetStatus(value ? Status.Unknown : Status.Ok);
            }
            else
            {
                // TODO: Remove ToString() from value, when Activity.Tags support bool value.
                this.activity.AddTag(key, value.ToString());
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

            // TODO: Remove ToString() from value, when Activity.Tags support int value.
            this.activity.AddTag(key, value.ToString());
            return this;
        }

        /// <inheritdoc/>
        public global::OpenTracing.ISpan SetTag(string key, double value)
        {
            if (key is null)
            {
                throw new ArgumentNullException(nameof(key));
            }

            // TODO: Remove ToString() from value, when Activity.Tags support double value.
            this.activity.AddTag(key, value.ToString());
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
