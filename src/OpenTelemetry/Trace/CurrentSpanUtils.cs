// <copyright file="CurrentSpanUtils.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace
{
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using OpenTelemetry.Context;

    internal static class CurrentSpanUtils
    {
        private static readonly ConditionalWeakTable<Activity, ScopeInSpan> ActivitySpanTable = new ConditionalWeakTable<Activity, ScopeInSpan>();

        public static ISpan CurrentSpan => CurrentScope.Span;

        public static IScope CurrentScope
        {
            get
            {
                var currentActivity = Activity.Current;
                if (currentActivity == null)
                {
                    return NoopScope.Instance;
                }

                if (ActivitySpanTable.TryGetValue(currentActivity, out var currentScope))
                {
                    return currentScope;
                }

                return NoopScope.Instance;
            }
        }

        public static IScope WithSpan(ISpan span, bool endSpan)
        {
            return new ScopeInSpan(span, endSpan);
        }

        private static void SetSpan(ScopeInSpan scope)
        {
            if (scope.ActualSpan.Activity == null)
            {
                // log error
                return;
            }

            if (ActivitySpanTable.TryGetValue(scope.ActualSpan.Activity, out _))
            {
                // log warning
                return;
            }

            ActivitySpanTable.Add(scope.ActualSpan.Activity, scope);
        }

        private static void DetachSpanFromActivity(Activity activity)
        {
            ActivitySpanTable.Remove(activity);
        }

        private sealed class ScopeInSpan : IScope
        {
            private readonly bool endSpan;

            private long disposed;

            public ScopeInSpan(ISpan span, bool endSpan)
            {
                this.endSpan = endSpan;

                if (span is Span spanImpl)
                {
                    this.ActualSpan = spanImpl;
                    if (spanImpl.OwnsActivity)
                    {
                        Activity.Current = spanImpl.Activity;
                    }

                    SetSpan(this);
                }
            }

            public ISpan Span => this.ActualSpan;

            public Span ActualSpan { get; }

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref this.disposed, 1, 0) == 1)
                {
                    return;
                }

                var current = this.ActualSpan;
                if (current == null)
                {
                    return;
                }

                bool safeToStopActivity = false;

                if (current.Activity == Activity.Current)
                {
                    if (!current.OwnsActivity)
                    {
                        DetachSpanFromActivity(current.Activity);
                    }
                    else
                    {
                        safeToStopActivity = true;
                    }
                }

                if (this.endSpan)
                {
                    current.End();
                }
                else if (safeToStopActivity)
                {
                    current.Activity.Stop();
                }
            }
        }
    }
}
