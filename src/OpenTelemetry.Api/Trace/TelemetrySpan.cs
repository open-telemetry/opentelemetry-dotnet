// <copyright file="TelemetrySpan.cs" company="OpenTelemetry Authors">
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
using System.Collections;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// <para>Span represents the execution of the certain span of code or span of time between two events which is part of
    /// a distributed trace and has result of execution, context of execution and other properties.</para>
    ///
    /// <para>This class is mostly write only. Span should not be used to exchange information. Only to add properties
    /// to it for monitoring purposes. It will be converted to SpanData that is readable.</para>
    /// </summary>
    public abstract class TelemetrySpan
    {
        /// <summary>
        /// Gets the span context.
        /// </summary>
        public abstract SpanContext Context { get; }

        /// <summary>
        /// Gets a value indicating whether this span will be recorded.
        /// </summary>
        public abstract bool IsRecording { get; }

        /// <summary>
        /// Sets the status of the span execution.
        /// </summary>
        public abstract Status Status { set; }

        /// <summary>
        /// Updates the <see cref="TelemetrySpan"/> name.
        ///
        /// If used, this will override the name provided via StartSpan method overload.
        /// Upon this update, any sampling behavior based on <see cref="TelemetrySpan"/> name will depend on the
        /// implementation.
        /// </summary>
        /// <param name="name">Name of the span.</param>
        public abstract void UpdateName(string name);

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value. The value may be an <see cref="IEnumerable"/> of primitive types. An enumeration may be iterated multiple times.</param>
        public abstract void SetAttribute(string key, object value);

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        public abstract void SetAttribute(string key, long value);

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        public abstract void SetAttribute(string key, bool value);

        /// <summary>
        /// Sets a new attribute on the span.
        /// </summary>
        /// <param name="key">Attribute key.</param>
        /// <param name="value">Attribute value.</param>
        public abstract void SetAttribute(string key, double value);

        /// <summary>
        /// Adds a single <see cref="Event"/> to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="name">Name of the <see cref="Event"/>.</param>
        public abstract void AddEvent(string name);

        /// <summary>
        /// Adds an <see cref="Event"/> instance to the <see cref="TelemetrySpan"/>.
        /// </summary>
        /// <param name="newEvent"><see cref="Event"/> to add to the span.</param>
        public abstract void AddEvent(Event newEvent);

        /// <summary>
        /// End the span.
        /// </summary>
        public abstract void End();

        /// <summary>
        /// End the span.
        /// </summary>
        /// <param name="endTimestamp">End timestamp.</param>
        public abstract void End(DateTimeOffset endTimestamp);
    }
}
