// <copyright file="EnrichmentScope.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using OpenTelemetry.Context;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// A class for enriching the data of <see cref="Activity"/> objects.
    /// </summary>
    public class EnrichmentScope : IDisposable
    {
        private static readonly RuntimeContextSlot<EnrichmentScope> RuntimeContextSlot = RuntimeContext.RegisterSlot<EnrichmentScope>("otel.enrichment_scope");

        private bool disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="EnrichmentScope"/> class.
        /// </summary>
        /// <param name="enrichmentAction">Callback action for performing <see cref="Activity"/> enrichment.</param>
        /// <param name="target">The target for the enrichment scope.</param>
        private EnrichmentScope(Action<Activity> enrichmentAction, EnrichmentScopeTarget target)
        {
            this.EnrichmentAction = enrichmentAction ?? throw new ArgumentNullException(nameof(enrichmentAction));
            this.EnrichmentTarget = target;

            this.Parent = Current;
            this.Parent.Child = this;

            RuntimeContextSlot.Set(this);
        }

        /// <summary>
        /// Gets the current <see cref="EnrichmentScope"/>.
        /// </summary>
        public static EnrichmentScope Current => RuntimeContextSlot.Get();

        /// <summary>
        /// Gets the parent <see cref="EnrichmentScope"/>.
        /// </summary>
        public EnrichmentScope Parent { get; internal set; }

        /// <summary>
        /// Gets the child <see cref="EnrichmentScope"/>.
        /// </summary>
        public EnrichmentScope Child { get; internal set; }

        /// <summary>
        /// Gets the enrichment action.
        /// </summary>
        public Action<Activity> EnrichmentAction { get; private set; }

        /// <summary>
        /// Gets the enrichment target.
        /// </summary>
        public EnrichmentScopeTarget EnrichmentTarget { get; private set; }

        /// <summary>
        /// Registers an action that will be called to enrich the next <see cref="Activity"/> processed under the current scope if it has been sampled.
        /// </summary>
        /// <param name="enrichmentAction">Action to be called.</param>
        /// <param name="target">The target for the enrichment scope.</param>
        /// <returns><see cref="IDisposable"/> to cancel the enrichment scope.</returns>
        public static IDisposable Begin(Action<Activity> enrichmentAction, EnrichmentScopeTarget target = EnrichmentScopeTarget.NextActivity)
            => new EnrichmentScope(enrichmentAction, target);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (!this.disposed)
            {
                this.EnrichmentAction = null;

                if (this.Parent.Child == this)
                {
                    this.Parent.Child = this.Child;
                }

                if (this.Child?.Parent == this)
                {
                    this.Child.Parent = this.Parent;
                }

                if (RuntimeContextSlot.Get() == this)
                {
                    var parent = this.Parent;
                    while (true)
                    {
                        if (parent == null || !parent.disposed)
                        {
                            RuntimeContextSlot.Set(parent);
                            break;
                        }

                        parent = parent.Parent;
                    }
                }

                this.disposed = true;
            }
        }
    }
}
