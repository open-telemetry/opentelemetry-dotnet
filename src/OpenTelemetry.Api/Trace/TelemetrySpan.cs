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
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

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
                return (this.Activity == null) ? false : this.Activity.IsAllDataRequested;
            }
        }

        /// <summary>
        /// Sets the status of the span execution.
        /// </summary>
        public Status Status
        {
            set
            {
                this.Activity?.SetStatus(value);
            }
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
        /// <param name="value">Attribute value. The value may be an <see cref="IEnumerable"/> of primitive types. An enumeration may be iterated multiple times.</param>
        /// <returns>The <see cref="TelemetrySpan"/> instance for chaining.</returns>
        /// <remarks>More types for value will be supported in the next release. (bool, int etc.)</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TelemetrySpan SetAttribute(string key, string value)
        {
            this.Activity?.AddTag(key, value);
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
            this.Activity?.AddEvent(new ActivityEvent(name, attributes));
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
            this.Activity?.AddEvent(new ActivityEvent(name, timestamp, attributes));
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

        /// <inheritdoc/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose()
        {
            this.Activity?.Dispose();
        }

        /// <summary>
        /// Marks the span as current.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void Activate()
        {
            Activity.Current = this.Activity;
        }
    }
}
