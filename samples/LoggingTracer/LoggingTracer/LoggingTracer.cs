// <copyright file="LoggingTracer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer
{
    using OpenTelemetry.Context;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;

    public class LoggingTracer : ITracer
    {
        public ISpan CurrentSpan => CurrentSpanUtils.CurrentSpan;

        public IBinaryFormat BinaryFormat => new LoggingBinaryFormat();

        public ITextFormat TextFormat => new LoggingTextFormat();

        public ISpanBuilder SpanBuilder(string spanName)
        {
            Logger.Log($"Tracer.SpanBuilder({spanName})");
            return new LoggingSpanBuilder(spanName, SpanKind.Internal);
        }

        public IScope WithSpan(ISpan span)
        {
            Logger.Log("Tracer.WithSpan");
            return new CurrentSpanUtils.LoggingScope(span);
        }
    }
}
