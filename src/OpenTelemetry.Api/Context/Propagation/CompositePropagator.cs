﻿// <copyright file="CompositePropagator.cs" company="OpenTelemetry Authors">
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
    /// CompositePropagator provides a mechanism for combining multiple propagators into a single one.
    /// </summary>
    public class CompositePropagator : IPropagator
    {
        private static readonly ISet<string> EmptyFields = new HashSet<string>();
        private readonly List<IPropagator> propagators;

        /// <summary>
        /// Initializes a new instance of the <see cref="CompositePropagator"/> class.
        /// </summary>
        /// <param name="propagators">List of <see cref="IPropagator"/> wire context propagator.</param>
        public CompositePropagator(IEnumerable<IPropagator> propagators)
        {
            this.propagators = new List<IPropagator>(propagators ?? throw new ArgumentNullException(nameof(propagators)));
        }

        /// <inheritdoc/>
        public ISet<string> Fields => EmptyFields;

        /// <inheritdoc/>
        public PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            foreach (var propagator in this.propagators)
            {
                context = propagator.Extract(context, carrier, getter);
            }

            return context;
        }

        /// <inheritdoc/>
        public void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
        {
            foreach (var propagator in this.propagators)
            {
                propagator.Inject(context, carrier, setter);
            }
        }
    }
}
