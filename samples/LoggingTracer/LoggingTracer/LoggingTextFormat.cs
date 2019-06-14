using System;
using System.Collections.Generic;
using OpenTelemetry.Trace;
using OpenTelemetry.Context.Propagation;

namespace LoggingTracer
{
    public sealed class LoggingTextFormat : ITextFormat
    {
        public ISet<string> Fields => throw new NotImplementedException();

        public SpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            Logger.Log($"LoggingTextFormat.Extract");
            return SpanContext.Blank;
        }

        public void Inject<T>(SpanContext spanContext, T carrier, Action<T, string, string> setter)
        {
            Logger.Log($"LoggingTextFormat.Inject");
        }
    }
}
