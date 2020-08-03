// <copyright file="BroadcastActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace.Internal
{
    internal class BroadcastActivityProcessor : ActivityProcessor
    {
        private readonly IEnumerable<ActivityProcessor> processors;
        private bool disposed;

        public BroadcastActivityProcessor(IEnumerable<ActivityProcessor> processors)
        {
            if (processors == null)
            {
                throw new ArgumentNullException(nameof(processors));
            }

            if (!processors.Any())
            {
                throw new ArgumentException($"{nameof(processors)} collection is empty");
            }

            this.processors = processors;
        }

        public override void OnEnd(Activity activity)
        {
            foreach (var processor in this.processors)
            {
                try
                {
                    processor.OnEnd(activity);
                }
                catch (Exception e)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException("OnEnd", e);
                }
            }
        }

        public override void OnStart(Activity activity)
        {
            foreach (var processor in this.processors)
            {
                try
                {
                    processor.OnStart(activity);
                }
                catch (Exception e)
                {
                    OpenTelemetrySdkEventSource.Log.SpanProcessorException("OnStart", e);
                }
            }
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>();
            foreach (var processor in this.processors)
            {
                tasks.Add(processor.ShutdownAsync(cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        public override Task ForceFlushAsync(CancellationToken cancellationToken)
        {
            var tasks = new List<Task>(this.processors.Count());
            foreach (var processor in this.processors)
            {
                tasks.Add(processor.ForceFlushAsync(cancellationToken));
            }

            return Task.WhenAll(tasks);
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            if (disposing && !this.disposed)
            {
                foreach (var processor in this.processors)
                {
                    try
                    {
                        if (processor is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                    }
                    catch (Exception e)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException("Dispose", e);
                    }
                }

                this.disposed = true;
            }
        }
    }
}
