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
using System;
using System.Threading;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    /// <summary>
    /// Span utils for Logging-only SDK implementation.
    /// </summary>
    internal static class CurrentSpanUtils
    {
        private static AsyncLocal<TelemetrySpan> asyncLocalContext = new AsyncLocal<TelemetrySpan>();

        public static TelemetrySpan CurrentSpan => asyncLocalContext.Value;

        public class LoggingScope : IDisposable
        {
            private readonly TelemetrySpan origContext;
            private readonly TelemetrySpan span;
            private readonly bool endSpan;

            public LoggingScope(TelemetrySpan span, bool endSpan = true)
            {
                this.span = span;
                this.endSpan = endSpan;
                this.origContext = CurrentSpanUtils.asyncLocalContext.Value;
                CurrentSpanUtils.asyncLocalContext.Value = span;
            }

            /// <inheritdoc/>
            public void Dispose()
            {
                Logger.Log("Scope.Dispose");
                var current = asyncLocalContext.Value;
                asyncLocalContext.Value = this.origContext;

                if (current != this.origContext)
                {
                    Logger.Log("Scope.Dispose: current != this.origContext");
                }

                if (this.endSpan)
                {
                    this.span.End();
                }
            }
        }
    }
}
