// <copyright file="MetricExporter.cs" company="OpenTelemetry Authors">
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
using System.Threading;

namespace OpenTelemetry.Metrics
{
    public abstract class MetricExporter : IDisposable
    {
        private int shutdownCount;

        public BaseProvider ParentProvider { get; internal set; }

        public abstract ExportResult Export(IEnumerable<Metric> metrics);

        public bool Shutdown(int timeoutMilliseconds = Timeout.Infinite)
        {
            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "timeoutMilliseconds should be non-negative.");
            }

            if (Interlocked.Increment(ref this.shutdownCount) > 1)
            {
                return false; // shutdown already called
            }

            try
            {
                return this.OnShutdown(timeoutMilliseconds);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public bool ForceFlush(int timeoutMilliseconds = Timeout.Infinite)
        {
            if (timeoutMilliseconds < 0 && timeoutMilliseconds != Timeout.Infinite)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutMilliseconds), timeoutMilliseconds, "timeoutMilliseconds should be non-negative.");
            }

            try
            {
                return this.OnForceFlush(timeoutMilliseconds);
            }
            catch (Exception)
            {
                return false;
            }
        }

        public virtual AggregationTemporality GetAggregationTemporality()
        {
            // TODO: One suggestion is to have SupportedTemporality
            // and PrefferedTemporality.
            // see https://github.com/open-telemetry/opentelemetry-dotnet/pull/2306#discussion_r701532743
            return AggregationTemporality.Cumulative;
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual bool OnShutdown(int timeoutMilliseconds)
        {
            return true;
        }

        protected virtual bool OnForceFlush(int timeoutMilliseconds)
        {
            return true;
        }

        protected virtual void Dispose(bool disposing)
        {
        }
    }
}
