// <copyright file="CompositeTextMapPropagator.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Context.Propagation
{
    /// <summary>
    /// CompositeTextMapPropagator provides a mechanism for combining multiple
    /// textmap propagators into a single one.
    /// </summary>
    public class CompositeTextMapPropagator : TextMapPropagator
    {
        private static readonly ISet<string> EmptyFields = new HashSet<string>();
        private readonly List<TextMapPropagator> propagators;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositeTextMapPropagator"/> class.
        /// </summary>
        /// <param name="propagators">List of <see cref="TextMapPropagator"/> wire context propagator.</param>
        public CompositeTextMapPropagator(IEnumerable<TextMapPropagator> propagators)
        {
            this.propagators = new List<TextMapPropagator>(propagators ?? throw new ArgumentNullException(nameof(propagators)));
        }

        /// <inheritdoc/>
        public override ISet<string> Fields => EmptyFields;

        /// <inheritdoc/>
        public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            foreach (var propagator in this.propagators)
            {
                context = propagator.Extract(context, carrier, getter);
            }

            return context;
        }

        /// <inheritdoc/>
        public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            foreach (var propagator in this.propagators)
            {
                propagator.Inject(context, carrier, setter);
            }
        }
    }
}
