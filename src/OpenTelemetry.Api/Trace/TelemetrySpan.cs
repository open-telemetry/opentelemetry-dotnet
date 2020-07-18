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

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// <para>Span represents the execution of the certain span of code or span of time between two events which is part of
    /// a distributed trace and has result of execution, context of execution and other properties.</para>
    /// </summary>
    public class TelemetrySpan : IDisposable
    {
        internal static TelemetrySpan NoOpInstance = new TelemetrySpan(null);
        private Activity activity;

        internal TelemetrySpan(Activity activity)
        {
            this.activity = activity;
        }

        /// <summary>
        /// Gets the span context.
        /// </summary>
        public SpanContext Context
        {
            get
            {
                if (this.activity == null)
                {
                    return default;
                }
                else
                {
                    return new SpanContext(this.activity.Context);
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
                return (this.activity == null) ? false : this.activity.IsAllDataRequested;
            }
        }

        /// <summary>
        /// Sets the status of the span execution.
        /// </summary>
        public Status Status
        {
            set
            {
                this.activity?.SetStatus(value);
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
        public void UpdateName(string name)
        {
            if (this.activity != null)
            {
                this.activity.DisplayName = name;
            }
        }

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value. The value may be an <see cref="IEnumerable"/> of primitive types. An enumeration may be iterated multiple times.</param>
        public void SetAttribute(string key, string value)
        {
            this.activity?.AddTag(key, value);
        }

        /// <summary>
        /// Adds a single <see cref="Event"/> to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the <see cref="Event"/>.</param>
        public void AddEvent(string name)
        {
            this.activity?.AddEvent(new Event(name).ActivityEvent);
        }

        /// <summary>
        /// Adds a single <see cref="Event"/> to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the <see cref="Event"/>.</param>
        /// <param name="timestamp">Timestamp of the <see cref="Event"/>.</param>
        public void AddEvent(string name, DateTimeOffset timestamp)
        {
            this.activity?.AddEvent(new Event(name, timestamp).ActivityEvent);
        }

        /// <summary>
        /// Adds a single <see cref="Event"/> to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the <see cref="Event"/>.</param>
        /// <param name="attributes">Attributes for the <see cref="Event"/>.</param>
        public void AddEvent(string name, IDictionary<string, object> attributes)
        {
            this.activity?.AddEvent(new Event(name, attributes).ActivityEvent);
        }

        /// <summary>
        /// Adds a single <see cref="Event"/> to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the <see cref="Event"/>.</param>
        /// <param name="timestamp">Timestamp of the <see cref="Event"/>.</param>
        /// <param name="attributes">Attributes for the <see cref="Event"/>.</param>
        public void AddEvent(string name, DateTimeOffset timestamp, IDictionary<string, object> attributes)
        {
            this.activity?.AddEvent(new Event(name, timestamp, attributes).ActivityEvent);
        }

        /// <summary>
        /// End the span.
        /// </summary>
        public void End()
        {
            this.activity?.Stop();
        }

        /// <summary>
        /// End the span.
        /// </summary>
        /// <param name="endTimestamp">End timestamp.</param>
        public void End(DateTimeOffset endTimestamp)
        {
            this.activity?.SetEndTime(endTimestamp.UtcDateTime);
            this.activity?.Stop();
        }

        /// <summary>
        /// Makes the span as current.
        /// </summary>
        public void Activate()
        {
            Activity.Current = this.activity;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.End();
        }
    }
}
