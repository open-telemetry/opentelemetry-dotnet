namespace LoggingTracer
{
    using System.Threading;
    using OpenTelemetry.Context;
    using OpenTelemetry.Trace;

    public static class CurrentSpanUtils
    {
        private static AsyncLocal<LoggingScope> asyncLocalContext = new AsyncLocal<LoggingScope>();

        public static IScope CurrentScope => asyncLocalContext.Value;

        public class LoggingScope : IScope
        {
            private readonly LoggingScope origContext;
            private readonly bool endSpan;

            public LoggingScope(ISpan span, bool endSpan = true)
            {
                this.Span = span;
                this.endSpan = endSpan;
                this.origContext = CurrentSpanUtils.asyncLocalContext.Value;
                CurrentSpanUtils.asyncLocalContext.Value = this;
            }

            public ISpan Span { get; }

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
                    this.Span.End();
                }
            }
        }
    }
}
