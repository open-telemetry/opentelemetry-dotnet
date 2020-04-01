// <copyright file="DistributedContext.cs" company="OpenTelemetry Authors">
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

using System;
using System.Linq;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Distributed context.
    /// </summary>
    public readonly struct DistributedContext : IEquatable<DistributedContext>
    {
        private static DistributedContextCarrier carrier = NoopDistributedContextCarrier.Instance;
        private readonly CorrelationContext correlationContext;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContext"/> struct.
        /// </summary>
        /// <param name="correlationContext">The correlation context.</param>
        internal DistributedContext(CorrelationContext correlationContext)
        {
            this.correlationContext = correlationContext;
        }

        /// <summary>
        /// Gets empty object of <see cref="DistributedContext"/> struct.
        /// </summary>
        public static DistributedContext Empty { get; } = new DistributedContext(CorrelationContext.Empty);

        /// <summary>
        /// Gets the current <see cref="CorrelationContext"/>.
        /// </summary>
        public static DistributedContext Current => carrier.Current;

        /// <summary>
        /// Gets or sets the default carrier instance of the <see cref="DistributedContextCarrier"/> class.
        /// SDK will need to override the value to AsyncLocalDistributedContextCarrier.Instance.
        /// </summary>
        public static DistributedContextCarrier Carrier
        {
            get => carrier;
            set
            {
                if (value is null)
                {
                    OpenTelemetryApiEventSource.Log.InvalidArgument("set_Carrier", nameof(value), "is null");
                }

                carrier = value ?? NoopDistributedContextCarrier.Instance;
            }
        }

        /// <summary>
        /// Gets the <see cref="CorrelationContext"/> for the current distributed context.
        /// </summary>
        public CorrelationContext CorrelationContext => this.correlationContext;

        /// <summary>
        /// Sets the current <see cref="DistributedContext"/>.
        /// </summary>
        /// <param name="context">Context to set as current.</param>
        /// <returns>Scope object. On disposal - original context will be restored.</returns>
        public static IDisposable SetCurrent(in DistributedContext context) => carrier.SetCurrent(context);

        /// <inheritdoc/>
        public bool Equals(DistributedContext other)
        {
            return this.CorrelationContext.Equals(other.CorrelationContext);
        }
    }
}
