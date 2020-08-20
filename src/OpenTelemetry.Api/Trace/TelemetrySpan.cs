// <copyright file="TelemetrySpan.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// <para>Span represents the execution of the certain span of code or span of time between two events which is part of
    /// a distributed trace and has result of execution, context of execution and other properties.</para>
    /// </summary>
    /// <remarks>Represents OpenTelemetry Span https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#span.</remarks>
    public class TelemetrySpan : IDisposable
    {
        internal static readonly TelemetrySpan NoopInstance = new TelemetrySpan(null);
        internal readonly Activity Activity;
        private static readonly IEnumerable<KeyValuePair<string, string>> EmptyBaggage = new KeyValuePair<string, string>[0];

        internal TelemetrySpan(Activity activity)
        {
            this.Activity = activity;
        }

        /// <summary>
        /// Gets the span context.
        /// </summary>
        public SpanContext Context
        {
            get
            {
                if (this.Activity == null)
                {
                    return default;
                }
                else
                {
                    return new SpanContext(this.Activity.Context);
                }
            }
        }

        /// <summary>
        /// Gets a value indicating whether this span will be recorded.
        /// </summary>
        public bool IsRecording
        {
            get
            {
                return this.Activity != null && this.Activity.IsAllDataRequested;
            }
        }

        /// <summary>
        /// Gets the span baggage.
        /// </summary>
        public IEnumerable<KeyValuePair<string, string>> Baggage => this.Activity?.Baggage ?? EmptyBaggage;

        /// <summary>
        /// Sets the status of the span execution.
        /// </summary>
        /// <param name="value">Status to be set.</param>
        public void SetStatus(Status value)
        {
            this.Activity?.SetStatus(value);
        }

        /// <summary>
        /// Updates the <see cref="TelemetrySpan"/> name.
        ///
        /// If used, this will override the name provided via StartSpan method overload.
        /// Upon this update, any sampling behavior based on <see cref="TelemetrySpan"/> name will depend on the
        /// implementation.
        /// </summary>
        /// <param name="name">Name of the span.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan UpdateName(string name)
        {
            if (this.Activity != null)
            {
                this.Activity.DisplayName = name;
            }

            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, string value)
        {
            this.Activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, int value)
        {
            this.Activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, bool value)
        {
            this.Activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, double value)
        {
            this.Activity?.SetTag(key, value);
            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="values">Attribute values.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, string[] values)
        {
            this.Activity?.SetTag(key, values);
            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="values">Attribute values.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, int[] values)
        {
            this.Activity?.SetTag(key, values);
            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="values">Attribute values.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, bool[] values)
        {
            this.Activity?.SetTag(key, values);
            return this;
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="values">Attribute values.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, double[] values)
        {
            this.Activity?.SetTag(key, values);
            return this;
        }

        /// <summary>
        /// Adds a single Event to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan AddEvent(string name)
        {
            this.Activity?.AddEvent(new ActivityEvent(name));
            return this;
        }

        /// <summary>
        /// Adds a single Event to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <param name="timestamp">Timestamp of the event.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan AddEvent(string name, DateTimeOffset timestamp)
        {
            this.Activity?.AddEvent(new ActivityEvent(name, timestamp));
            return this;
        }

        /// <summary>
        /// Adds a single Event to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <param name="attributes">Attributes for the event.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan AddEvent(string name, IDictionary<string, object> attributes)
        {
            ActivityTagsCollection eventTags = new ActivityTagsCollection(attributes);
            this.Activity?.AddEvent(new ActivityEvent(name, default, eventTags));
            return this;
        }

        /// <summary>
        /// Adds a single Event to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the event.</param>
        /// <param name="timestamp">Timestamp of the event.</param>
        /// <param name="attributes">Attributes for the event.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan AddEvent(string name, DateTimeOffset timestamp, IDictionary<string, object> attributes)
        {
            var eventTags = new ActivityTagsCollection(attributes);
            this.Activity?.AddEvent(new ActivityEvent(name, timestamp, eventTags));
            return this;
        }

        /// <summary>
        /// End the span.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End()
        {
            this.Activity?.Stop();
        }

        /// <summary>
        /// End the span.
        /// </summary>
        /// <param name="endTimestamp">End timestamp.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void End(DateTimeOffset endTimestamp)
        {
            this.Activity?.SetEndTime(endTimestamp.UtcDateTime);
            this.Activity?.Stop();
        }

        /// <summary>
        /// Retrieves a baggage item.
        /// </summary>
        /// <param name="key">Baggage item key.</param>
        /// <returns>Retrieved baggage value or <see langword="null"/> if no match was found.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string GetBaggageItem(string key)
        {
            return this.Activity?.GetBaggageItem(key);
        }

        /// <summary>
        /// Adds a baggage item to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="key">Baggage item key.</param>
        /// <param name="value">Baggage item value.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan AddBaggage(string key, string value)
        {
            this.Activity?.AddBaggage(key, value);

            return this;
        }

        /// <summary>
        /// Record Exception.
        /// </summary>
        /// <param name="ex">Exception to be recorded.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan RecordException(Exception ex)
        {
            if (ex == null)
            {
                return this;
            }

            return this.RecordException(ex.GetType().Name, ex.Message, ex.ToInvariantString());
        }

        /// <summary>
        /// Record Exception.
        /// </summary>
        /// <param name="type">Type of the exception to be recorded.</param>
        /// <param name="message">Message of the exception to be recorded.</param>
        /// <param name="stacktrace">Stacktrace of the exception to be recorded.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        public TelemetrySpan RecordException(string type, string message, string stacktrace)
        {
            Dictionary<string, object> attributes = new Dictionary<string, object>();

            if (!string.IsNullOrWhiteSpace(type))
            {
                attributes.Add(SemanticConventions.AttributeExceptionType, type);
            }

            if (!string.IsNullOrWhiteSpace(stacktrace))
            {
                attributes.Add(SemanticConventions.AttributeExceptionStacktrace, stacktrace);
            }

            if (!string.IsNullOrWhiteSpace(message))
            {
                attributes.Add(SemanticConventions.AttributeExceptionMessage, message);
            }

            if (attributes.Count != 0)
            {
                this.AddEvent(SemanticConventions.AttributeExceptionEventName, attributes);
            }

            return this;
        }

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Marks the span as current.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Activate()
        {
            Activity.Current = this.Activity;
        }

        /// <summary>
        /// Releases the unmanaged resources used by this class and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing"><see langword="true"/> to release both managed and unmanaged resources; <see langword="false"/> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            this.Activity?.Dispose();
        }
    }
}
