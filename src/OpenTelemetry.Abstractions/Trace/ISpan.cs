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
    /// a distributed trace and has result of execution, context of executuion and other properties.</para>
    ///
    /// <para>This class is mostly write only. Span should not be used to exchange information. Only to add properties
    /// to it for monitoring purposes. It will be converted to SpanData that is readable.</para>
    /// </summary>
    public interface ISpan
    {
        /// <summary>
        /// Gets or sets the span name.
        /// </summary>
        string Name { get; set; }

        /// <summary>
        /// Gets the span context.
        /// </summary>
        ISpanContext Context { get; }

        /// <summary>
        /// Gets the span options.
        /// </summary>
        SpanOptions Options { get; }

        /// <summary>
        /// Gets or sets the status of the span execution.
        /// </summary>
        Status Status { get; set; }

        /// <summary>
        /// Gets or sets the kind of a span.
        /// </summary>
        SpanKind? Kind { get; set; }

        /// <summary>
        /// Gets a value indicating whether this span was already stopped.
        /// </summary>
        bool HasEnded { get; }

        /// <summary>
        /// Puts a new attribute to the span.
        /// </summary>
        /// <param name="key">Key of the attribute.</param>
        /// <param name="value">Attribute value.</param>
        void SetAttribute(string key, IAttributeValue value);

        /// <summary>
        /// Puts a list of attributes to the span.
        /// </summary>
        /// <param name="attributes">Collection of attributes name/value pairs.</param>
        void SetAttributes(IDictionary<string, IAttributeValue> attributes);

        /// <summary>
        /// Adds a single <see cref="IEvent"/> to the <see cref="ISpan"/>.
        /// </summary>
        /// <param name="name">Name of the <see cref="IEvent"/>.</param>
        void AddEvent(string name);

        /// <summary>
        /// Adds a single <see cref="IEvent"/> with the <see cref="IDictionary{String, IAttributeValue}"/> attributes to the <see cref="ISpan"/>.
        /// </summary>
        /// <param name="name">Event name.</param>
        /// <param name="attributes"><see cref="IDictionary{String, IAttributeValue}"/> of attributes name/value pairs associted with the <see cref="IEvent"/>.</param>
        void AddEvent(string name, IDictionary<string, IAttributeValue> attributes);

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
        /// Complete the span and set end span options.
        /// </summary>
        /// <param name="options">Span completion options.</param>
        void End(EndSpanOptions options);

        /// <summary>
        /// End the span.
        /// </summary>
        void End();
    }
}
