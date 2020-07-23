// <copyright file="ActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Trace
{
    /// <summary>
    /// Activity processor base class.
    /// </summary>
    public abstract class ActivityProcessor
    {
        /// <summary>
        /// Activity start hook.
        /// </summary>
        /// <param name="activity">Instance of activity to process.</param>
        public abstract void OnStart(Activity activity);

        /// <summary>
        /// Activity end hook.
        /// </summary>
        /// <param name="activity">Instance of activity to process.</param>
        public abstract void OnEnd(Activity activity);

        /// <summary>
        /// Shuts down Activity processor asynchronously.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns <see cref="Task"/>.</returns>
        public abstract Task ShutdownAsync(CancellationToken cancellationToken);

        /// <summary>
        /// Flushes all activities that have not yet been processed.
        /// </summary>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Returns <see cref="Task"/>.</returns>
        public abstract Task ForceFlushAsync(CancellationToken cancellationToken);
    }
}
