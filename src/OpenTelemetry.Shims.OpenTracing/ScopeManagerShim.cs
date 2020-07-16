// <copyright file="ScopeManagerShim.cs" company="OpenTelemetry Authors">
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
using System.Runtime.CompilerServices;
using System.Threading;
using global::OpenTracing;
using OpenTelemetry.Trace;

namespace OpenTelemetry.Shims.OpenTracing
{
    public sealed class ScopeManagerShim : IScopeManager
    {
        private static readonly ConditionalWeakTable<Activity, global::OpenTracing.IScope> SpanScopeTable = new ConditionalWeakTable<Activity, global::OpenTracing.IScope>();

        private readonly ActivitySource activitySource;

#if DEBUG
        private int spanScopeTableCount;
#endif

        public ScopeManagerShim(ActivitySource source)
        {
            this.activitySource = source ?? throw new ArgumentNullException(nameof(source));
        }

#if DEBUG
        public int SpanScopeTableCount => this.spanScopeTableCount;
#endif

        /// <inheritdoc/>
        public global::OpenTracing.IScope Active
        {
            get
            {
                var currentActivity = Activity.Current;
                if (currentActivity == null || !currentActivity.Context.IsValid())
                {
                    return null;
                }

                if (SpanScopeTable.TryGetValue(currentActivity, out var openTracingScope))
                {
                    return openTracingScope;
                }

                return new ScopeInstrumentation(currentActivity);
            }
        }

        /// <inheritdoc/>
        public global::OpenTracing.IScope Activate(ISpan span, bool finishSpanOnDispose)
        {
            if (!(span is ActivityShim shim))
            {
                throw new ArgumentException("span is not a valid SpanShim object");
            }

            var scope = this.activitySource.StartActivity(shim.ActivityObj.OperationName);

            var instrumentation = new ScopeInstrumentation(
                shim.ActivityObj,
                () =>
                {
                    var removed = SpanScopeTable.Remove(shim.ActivityObj);
#if DEBUG
                    if (removed)
                    {
                        Interlocked.Decrement(ref this.spanScopeTableCount);
                    }
#endif
                    scope.Dispose();
                });

            SpanScopeTable.Add(shim.ActivityObj, instrumentation);
#if DEBUG
            Interlocked.Increment(ref this.spanScopeTableCount);
#endif

            return instrumentation;
        }

        private class ScopeInstrumentation : global::OpenTracing.IScope
        {
            private readonly Action disposeAction;

            public ScopeInstrumentation(Activity activity, Action disposeAction = null)
            {
                this.Span = new ActivityShim(activity);
                this.disposeAction = disposeAction;
            }

            /// <inheritdoc/>
            public ISpan Span { get; }

            /// <inheritdoc/>
            public void Dispose()
            {
                this.disposeAction?.Invoke();
            }
        }
    }
}
