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
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Tests.Shared
{
    public class TestActivityProcessor : ActivityProcessor
    {
        private readonly bool collectEndedSpans;
        private readonly List<Activity> endedActivityObjects = new List<Activity>();

        public TestActivityProcessor(Action<Activity> onStartAction = null, Action<Activity> onEndAction = null, bool collectEndedSpans = false)
        {
            this.StartAction = onStartAction;
            this.EndAction = onEndAction;
            this.collectEndedSpans = collectEndedSpans;
        }

        public Action<Activity> StartAction { get; set; }

        public Action<Activity> EndAction { get; set; }

        public bool ShutdownCalled { get; private set; }

        public bool ForceFlushCalled { get; private set; }

        public bool DisposedCalled { get; private set; }

        public IList<Activity> EndedActivityObjects => this.endedActivityObjects;

        public override void OnStart(Activity span)
        {
            this.StartAction?.Invoke(span);
        }

        public override void OnEnd(Activity span)
        {
            this.EndAction?.Invoke(span);

            if (this.collectEndedSpans)
            {
                this.endedActivityObjects.Add(span);
            }
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

        public IDisposable ResetWhenDone() => new ResetScope(this);

        public void Reset()
        {
            this.endedActivityObjects.Clear();
            this.ShutdownCalled = false;
            this.ForceFlushCalled = false;
            this.DisposedCalled = false;
        }

        protected override void Dispose(bool disposing)
        {
            this.DisposedCalled = true;
        }

        private class ResetScope : IDisposable
        {
            private readonly TestActivityProcessor testActivityProcessor;

            public ResetScope(TestActivityProcessor testActivityProcessor)
            {
                this.testActivityProcessor = testActivityProcessor;
            }

            public void Dispose()
            {
                this.testActivityProcessor.Reset();
            }
        }
    }
}
