// <copyright file="TestActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Exporter.Jaeger.Tests
{
    public class TestActivityProcessor : ActivityProcessor, IDisposable
    {
        public Action<Activity> StartAction;
        public Action<Activity> EndAction;

        public TestActivityProcessor()
        {
        }

        public TestActivityProcessor(Action<Activity> onStart, Action<Activity> onEnd)
        {
            this.StartAction = onStart;
            this.EndAction = onEnd;
        }

        public bool ShutdownCalled { get; private set; } = false;

        public bool ForceFlushCalled { get; private set; } = false;

        public bool DisposedCalled { get; private set; } = false;

        public override void OnStart(Activity activity)
        {
            this.StartAction?.Invoke(activity);
        }

        public override void OnEnd(Activity activity)
        {
            this.EndAction?.Invoke(activity);
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            this.ShutdownCalled = true;
#if NET452
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        public override Task ForceFlushAsync(CancellationToken cancellationToken)
        {
            this.ForceFlushCalled = true;
#if NET452
            return Task.FromResult(0);
#else
            return Task.CompletedTask;
#endif
        }

        protected override void Dispose(bool disposing)
        {
            this.DisposedCalled = true;
        }
    }
}
