// <copyright file="CurrentSpanUtils.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer
{
    using System.Threading;
    using OpenTelemetry.Context;
    using OpenTelemetry.Trace;

    /// <summary>
    /// Span utils for Logging-only SDK implementation.
    /// </summary>
    internal static class CurrentSpanUtils
    {
        private static AsyncLocal<ISpan> asyncLocalContext = new AsyncLocal<ISpan>();

        public static ISpan CurrentSpan => asyncLocalContext.Value;

        public class LoggingScope : IScope
        {
            private readonly ISpan origContext;
            private readonly ISpan span;
            private readonly bool endSpan;

            public LoggingScope(ISpan span, bool endSpan = true)
            {
                this.span = span;
                this.endSpan = endSpan;
                this.origContext = CurrentSpanUtils.asyncLocalContext.Value;
                CurrentSpanUtils.asyncLocalContext.Value = span;
            }

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
