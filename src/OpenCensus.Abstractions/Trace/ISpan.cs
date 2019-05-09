// <copyright file="ISpan.cs" company="OpenCensus Authors">
// Copyright 2018, OpenCensus Authors
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

namespace OpenCensus.Trace
{
    using System.Collections.Generic;

    /// <summary>
    /// Span represents the execution of the certain span of code or span of time between two events which is part of
    /// a distributed trace and has result of execution, context of executuion and other properties.
    ///
    /// This class is mostly write only. Span should not be used to exchange information. Only to add properties
    /// to it for monitoring purposes. It will be converted to SpanData that is readable.
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
        void PutAttribute(string key, IAttributeValue value);

        /// <summary>
        /// Puts a list of attributes to the span.
        /// </summary>
        /// <param name="attributes">Collection of attributes name/value pairs.</param>
        void PutAttributes(IDictionary<string, IAttributeValue> attributes);

        /// <summary>
        /// Adds a single annotation to the span.
        /// </summary>
        /// <param name="description">Annotation description.</param>
        void AddAnnotation(string description);

        /// <summary>
        /// Adds a single annotation with the attributes to the span.
        /// </summary>
        /// <param name="description">Annotation description.</param>
        /// <param name="attributes">Collection of attributes name/value pairs associted with the annotation.</param>
        void AddAnnotation(string description, IDictionary<string, IAttributeValue> attributes);

        /// <summary>
        /// Adds an annotation to the span.
        /// </summary>
        /// <param name="annotation">Annotation to add to the span.</param>
        void AddAnnotation(IAnnotation annotation);

        /// <summary>
        /// Adds the message even to the span.
        /// </summary>
        /// <param name="messageEvent">Message event to add to the span.</param>
        void AddMessageEvent(IMessageEvent messageEvent);

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
