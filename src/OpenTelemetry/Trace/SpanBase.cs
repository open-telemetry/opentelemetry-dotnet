// <copyright file="SpanBase.cs" company="OpenTelemetry Authors">
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
    using System;
    using System.Collections.Generic;
    using OpenTelemetry.Trace.Export;
    using OpenTelemetry.Utils;

    /// <summary>
    /// Span base class.
    /// </summary>
    public abstract class SpanBase : ISpan, IElement<SpanBase>
    {
        private static readonly IDictionary<string, IAttributeValue> EmptyAttributes = new Dictionary<string, IAttributeValue>();

        internal SpanBase()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpanBase"/> class.
        /// </summary>
        /// <param name="context">Span context.</param>
        /// <param name="options">Span creation options.</param>
        protected SpanBase(ISpanContext context, SpanOptions options = SpanOptions.None)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context.TraceOptions.IsSampled && !options.HasFlag(SpanOptions.RecordEvents))
            {
                throw new ArgumentOutOfRangeException("Span is sampled, but does not have RECORD_EVENTS set.");
            }

            this.Context = context;
            this.Options = options;
        }

        public abstract string Name { get; set; }

        /// <inheritdoc/>
        public virtual ISpanContext Context { get; }

        /// <inheritdoc/>
        public virtual SpanOptions Options { get; }

        /// <inheritdoc/>
        public abstract Status Status { get; set; }

        /// <inheritdoc/>
        public abstract SpanKind? Kind { get; set; }

        /// <inheritdoc/>
        public SpanBase Next { get; set; }

        /// <inheritdoc/>
        public SpanBase Previous { get; set; }

        /// <summary>
        /// Gets the span end time.
        /// </summary>
        public abstract DateTimeOffset EndTime { get; }

        /// <summary>
        /// Gets the latency (difference beteen stat and end time).
        /// </summary>
        public abstract TimeSpan Latency { get; }

        /// <summary>
        /// Gets a value indicating whether span stored in local store.
        /// </summary>
        public abstract bool IsSampleToLocalSpanStore { get; }

        /// <summary>
        /// Gets the parent span id.
        /// </summary>
        public abstract ISpanId ParentSpanId { get; }

        /// <summary>
        /// Gets a value indicating whether this span was already stopped.
        /// </summary>
        public abstract bool HasEnded { get; }

        /// <inheritdoc/>
        public virtual void PutAttribute(string key, IAttributeValue value)
        {
            this.PutAttributes(new Dictionary<string, IAttributeValue>() { { key, value } });
        }

        /// <inheritdoc/>
        public abstract void PutAttributes(IDictionary<string, IAttributeValue> attributes);

        /// <inheritdoc/>
        public void AddAnnotation(string description)
        {
            this.AddAnnotation(description, EmptyAttributes);
        }

        /// <inheritdoc/>
        public abstract void AddAnnotation(string description, IDictionary<string, IAttributeValue> attributes);

        /// <inheritdoc/>
        public abstract void AddAnnotation(IAnnotation annotation);

        /// <inheritdoc/>
        public abstract void AddMessageEvent(IMessageEvent messageEvent);

        /// <inheritdoc/>
        public abstract void AddLink(ILink link);

        /// <inheritdoc/>
        public abstract void End(EndSpanOptions options);

        /// <inheritdoc/>
        public void End()
        {
            this.End(EndSpanOptions.Default);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return "Span[" +
                this.Name +
                "]";
        }

        /// <summary>
        /// Converts this span into span data for exporting purposes.
        /// </summary>
        /// <returns>Span Data corresponding current span.</returns>
        public abstract ISpanData ToSpanData();
    }
}
