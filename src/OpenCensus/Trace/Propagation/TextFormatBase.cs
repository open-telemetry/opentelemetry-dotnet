// <copyright file="TextFormatBase.cs" company="OpenCensus Authors">
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

namespace OpenCensus.Trace.Propagation
{
    using System;
    using System.Collections.Generic;
    using OpenCensus.Trace.Propagation.Implementation;

    /// <summary>
    /// Text format wire context propagator. Helps to extract and inject context from textual
    /// representation (typically http headers or metadata colleciton).
    /// </summary>
    public abstract class TextFormatBase : ITextFormat
    {
        private static readonly NoopTextFormat NoopTextFormatInstance = new NoopTextFormat();

        /// <inheritdoc/>
        public abstract ISet<string> Fields { get; }

        internal static ITextFormat NoopTextFormat
        {
            get
            {
                return NoopTextFormatInstance;
            }
        }

        /// <inheritdoc/>
        public abstract ISpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter);

        /// <inheritdoc/>
        public abstract void Inject<T>(ISpanContext spanContext, T carrier, Action<T, string, string> setter);
    }
}
