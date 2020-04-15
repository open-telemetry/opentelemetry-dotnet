// <copyright file="DistributedContextBuilder.cs" company="OpenTelemetry Authors">
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
using System.Collections.Generic;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Distributed context Builder.
    /// </summary>
    public struct DistributedContextBuilder
    {
        private CorrelationContextBuilder correlationContextBuilder;

        /// <summary>
        /// Initializes a new instance of the <see cref="DistributedContextBuilder"/> struct.
        /// </summary>
        /// <param name="inheritCurrentContext">Flag to allow inheriting the current context entries.</param>
        public DistributedContextBuilder(bool inheritCurrentContext)
        {
            this.correlationContextBuilder = new CorrelationContextBuilder(false);

            if (DistributedContext.Carrier is NoopDistributedContextCarrier)
            {
                return;
            }

            if (inheritCurrentContext)
            {
                this.correlationContextBuilder.Add(DistributedContext.Current.CorrelationContext.Entries);
            }
        }

        /// <summary>
        /// Create context.
        /// </summary>
        /// <param name="key">The correlation key.</param>
        /// <param name="value">The correlation value.</param>
        /// <returns>A <see cref="DistributedContext"/> instance.</returns>
        public static DistributedContext CreateContext(string key, string value) =>
            new DistributedContext(new CorrelationContextBuilder(inheritCurrentContext: false).Add(key, value).Build());

        /// <summary>
        /// Create context.
        /// </summary>
        /// <param name="entries">A list of correlations to create the context with.</param>
        /// <returns>A <see cref="DistributedContext"/> instance.</returns>
        public static DistributedContext CreateContext(IEnumerable<CorrelationContextEntry> entries) =>
            new DistributedContext(new CorrelationContextBuilder(inheritCurrentContext: false).Add(entries).Build());

        /// <summary>
        /// Configures correlations to be used with the context.
        /// </summary>
        /// <param name="configureCorrelations">An <see cref="Action{CorrelationContextBuilder}"/> used to configure correlations.</param>
        /// <returns>The current <see cref="DistributedContextBuilder"/> instance.</returns>
        public DistributedContextBuilder Correlations(Action<CorrelationContextBuilder> configureCorrelations)
        {
            configureCorrelations?.Invoke(this.correlationContextBuilder);
            return this;
        }

        /// <summary>
        /// Build a Distributed Context from current builder.
        /// </summary>
        /// <returns><see cref="DistributedContext"/> instance.</returns>
        public DistributedContext Build()
        {
            if (DistributedContext.Carrier is NoopDistributedContextCarrier)
            {
                return DistributedContext.Empty;
            }

            return new DistributedContext(this.correlationContextBuilder.Build());
        }
    }
}
