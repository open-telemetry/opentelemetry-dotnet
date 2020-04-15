// <copyright file="AsyncLocalDistributedContextCarrier.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Context
{
    /// <summary>
    /// Distributed Context carrier using AsyncLocal.
    /// </summary>
    public sealed class AsyncLocalDistributedContextCarrier : DistributedContextCarrier
    {
        private static AsyncLocal<DistributedContext> carrier = new AsyncLocal<DistributedContext>();

        private AsyncLocalDistributedContextCarrier()
        {
            this.OverwriteCurrent(DistributedContext.Empty);
        }

        /// <summary>
        /// Gets the instance of <see cref="AsyncLocalDistributedContextCarrier"/>.
        /// </summary>
        public static DistributedContextCarrier Instance { get; } = new AsyncLocalDistributedContextCarrier();

        /// <summary>
        /// Gets the current <see cref="DistributedContext"/>.
        /// </summary>
        public override DistributedContext Current => carrier.Value;

        /// <summary>
        /// Sets the current <see cref="DistributedContext"/>.
        /// </summary>
        /// <param name="context">Context to set as current.</param>
        /// <returns>Scope object. On disposal - original context will be restored.</returns>
        public override IDisposable SetCurrent(in DistributedContext context) => new DistributedContextState(in context);

        internal void OverwriteCurrent(in DistributedContext context) => carrier.Value = context;
    }
}
