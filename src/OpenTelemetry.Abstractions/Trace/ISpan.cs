// <copyright file="ISpan.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System.Collections.Generic;

    /// <summary>
    /// <para>Span represents the execution of the certain span of code or span of time between two events which is part of
    /// a distributed trace and has result of execution, context of execution and other properties.</para>
    ///
    /// <para>This class is mostly write only. Span should not be used to exchange information. Only to add properties
    /// to it for monitoring purposes. It will be converted to SpanData that is readable.</para>
    /// </summary>
    public interface ISpan
    {
        /// <summary>
        /// Gets the span context.
        /// </summary>
        SpanContext Context { get; }

        /// <summary>
        /// Gets a value indicating whether this span will be recorded.
        /// </summary>
        bool IsRecordingEvents { get; }

        /// <summary>
        /// Sets the status of the span execution.
        /// </summary>
        Status Status { set; }

        /// <summary>
        /// Gets a value indicating whether this span was already stopped.
        /// </summary>
        /// <remarks>This method is not compliant with the specification. https://github.com/open-telemetry/opentelemetry-specification/issues/55 .</remarks>
        bool HasEnded { get; }

        /// <summary>
        /// Updates the <see cref="ISpan"/> name.
        ///
        /// If used, this will override the name provided via <see cref="ISpanBuilder"/>.
        /// Upon this update, any sampling behavior based on <see cref="ISpan"/> name will depend on the
        /// implementation.
        /// </summary>
        /// <param name="name">Name of the span.</param>
        void UpdateName(string name);

        /// <summary>
        /// Puts a new attribute to the span.
        /// </summary>
        /// <param name="key">Key of the attribute.</param>
        /// <param name="value">Attribute value.</param>
        void SetAttribute(string key, string value);

        /// <summary>
        /// Puts a new attribute to the span.
        /// </summary>
        /// <param name="key">Key of the attribute.</param>
        /// <param name="value">Attribute value.</param>
        void SetAttribute(string key, long value);

        /// <summary>
        /// Puts a new attribute to the span.
        /// </summary>
        /// <param name="key">Key of the attribute.</param>
        /// <param name="value">Attribute value.</param>
        void SetAttribute(string key, double value);

        /// <summary>
        /// Puts a new attribute to the span.
        /// </summary>
        /// <param name="key">Key of the attribute.</param>
        /// <param name="value">Attribute value.</param>
        void SetAttribute(string key, bool value);

        void SetAttribute(KeyValuePair<string, object> keyValuePair);

        /// <summary>
        /// Adds a single <see cref="IEvent"/> to the <see cref="ISpan"/>.
        /// </summary>
        /// <param name="name">Name of the <see cref="IEvent"/>.</param>
        void AddEvent(string name);

        /// <summary>
        /// Adds a single <see cref="IEvent"/> with the <see cref="IDictionary{String, IAttributeValue}"/> attributes to the <see cref="ISpan"/>.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="attributes"><see cref="IDictionary{String, IAttributeValue}"/> of attributes name/value pairs associated with the <see cref="IEvent"/>.</param>
        void AddEvent(string name, IDictionary<string, object> attributes);

        /// <summary>
        /// Adds an <see cref="IEvent"/> object to the <see cref="ISpan"/>.
        /// </summary>
        /// <param name="newEvent"><see cref="IEvent"/> to add to the span.</param>
        void AddEvent(IEvent newEvent);

        /// <summary>
        /// Adds link to the span.
        /// </summary>
        /// <param name="link">Link to add to the span.</param>
        void AddLink(ILink link);

        /// <summary>
        /// End the span.
        /// </summary>
        void End();
    }
}
