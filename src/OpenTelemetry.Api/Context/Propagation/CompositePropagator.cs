// <copyright file="CompositePropagator.cs" company="OpenTelemetry Authors">
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
using System.Linq;

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// CompositePropagator provides a mechanism for combining multiple propagators into a single one.
    /// </summary>
    public class CompositePropagator : ITextFormat
    {
        private static readonly ISet<string> EmptyFields = new HashSet<string>();
        private readonly List<ITextFormat> textFormats;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositePropagator"/> class.
        /// </summary>
        /// <param name="textFormats">List of <see cref="ITextFormat"/> wire context propagator.</param>
        public CompositePropagator(List<ITextFormat> textFormats)
        {
            this.textFormats = textFormats ?? throw new ArgumentNullException(nameof(textFormats));
        }

        /// <inheritdoc/>
        public ISet<string> Fields => EmptyFields;

        /// <inheritdoc/>
        public ActivityContext Extract<T>(ActivityContext activityContext, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            foreach (var textFormat in this.textFormats)
            {
                activityContext = textFormat.Extract(activityContext, carrier, getter);
                if (activityContext.IsValid())
                {
                    return activityContext;
                }
            }

            return activityContext;
        }

        /// <inheritdoc/>
        public void Inject<T>(ActivityContext activityContext, T carrier, Action<T, string, string> setter)
        {
            foreach (var textFormat in this.textFormats)
            {
                textFormat.Inject(activityContext, carrier, setter);
            }
        }

        /// <inheritdoc/>
        public bool IsInjected<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            return this.textFormats.All(textFormat => textFormat.IsInjected(carrier, getter));
        }
    }
}
