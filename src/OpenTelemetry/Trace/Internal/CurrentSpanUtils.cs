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

namespace OpenTelemetry.Trace.Internal
{
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using OpenTelemetry.Context;

    internal static class CurrentSpanUtils
    {
        private static readonly ConditionalWeakTable<Activity, ISpan> ActivitySpanTable = new ConditionalWeakTable<Activity, ISpan>();

        public static ISpan CurrentSpan
        {
            get
            {
                var currentActivity = Activity.Current;
                if (currentActivity == null)
                {
                    return BlankSpan.Instance;
                }

                if (ActivitySpanTable.TryGetValue(currentActivity, out var currentSpan))
                {
                    return currentSpan;
                }

                return BlankSpan.Instance;
            }
        }

        public static IScope WithSpan(ISpan span, bool endSpan)
        {
            return new ScopeInSpan(span, endSpan);
        }

        private static void SetSpan(Span span)
        {
            if (span.Activity == null)
            {
                // log error
                return;
            }

            if (ActivitySpanTable.TryGetValue(span.Activity, out _))
            {
                // log warning
                return;
            }

            ActivitySpanTable.Add(span.Activity, span);
        }

        private static void DetachSpanFromActivity(Activity activity)
        {
            ActivitySpanTable.Remove(activity);
        }

        private sealed class ScopeInSpan : IScope
        {
            private readonly ISpan span;
            private readonly bool endSpan;

            public ScopeInSpan(ISpan span, bool endSpan)
            {
                this.span = span;
                this.endSpan = endSpan;

                if (span is Span spanImpl)
                {
                    if (spanImpl.OwnsActivity)
                    {
                        Activity.Current = spanImpl.Activity;
                    }

                    SetSpan(spanImpl);
                }
            }

            public void Dispose()
            {
                bool safeToStopActivity = false;
                var current = (Span)this.span;
                if (current != null && current.Activity == Activity.Current)
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
                    this.span.End();
                }
                else if (safeToStopActivity)
                {
                    current.Activity.Stop();
                }
            }
        }
    }
}
