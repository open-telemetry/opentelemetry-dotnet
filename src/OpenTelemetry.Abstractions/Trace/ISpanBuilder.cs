// <copyright file="ISpanBuilder.cs" company="OpenTelemetry Authors">
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
    using OpenTelemetry.Common;

    /// <summary>
    /// Span builder.
    /// </summary>
    public interface ISpanBuilder
    {
        /// <summary>
        /// Set the sampler for the span.
        /// </summary>
        /// <param name="sampler">Sampler to use to build span.</param>
        /// <returns>This span builder for chaining.</returns>
        ISpanBuilder SetSampler(ISampler sampler);

        /// <summary>
        /// Set the parent links on the span.
        /// </summary>
        /// <param name="parentLinks">Parent links to set on span.</param>
        /// <returns>This span builder for chaining.</returns>
        ISpanBuilder SetParentLinks(IEnumerable<ISpan> parentLinks);

        /// <summary>
        /// Set the record events value.
        /// </summary>
        /// <param name="recordEvents">Value indicating whether to record span.</param>
        /// <returns>This span builder for chaining.</returns>
        ISpanBuilder SetRecordEvents(bool recordEvents);

        /// <summary>
        /// Starts the span.
        /// </summary>
        /// <returns>Span that was just started.</returns>
        ISpan StartSpan();

        /// <summary>
        /// Starts the span and set it as a current on the current context.
        /// </summary>
        /// <returns>Scoped event to control the scope of span in the context.
        /// Dispose to stop the span and disassiciate it from the current context.</returns>
        IScope StartScopedSpan();

        /// <summary>
        /// Starts the span and set it as a current on the current context, setting the out param to current span.
        /// </summary>
        /// <param name="currentSpan">Current span.</param>
        /// <returns>Scoped event to control the scope of span in the context.
        /// Dispose to stop the span and disassiciate it from the current context.</returns>
        IScope StartScopedSpan(out ISpan currentSpan);
    }
}
