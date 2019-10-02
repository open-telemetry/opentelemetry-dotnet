// <copyright file="LoggingTracer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

using System.Linq;
using OpenTelemetry.Resources;

namespace LoggingTracer
{
    using System;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;

    public class LoggingTracer : ITracer
    {
        private string prefix;

        internal LoggingTracer(Resource libraryResource)
        {
            this.prefix = "Tracer(" + string.Join(", ", libraryResource.Labels.Select(l => l.Value).ToArray()) + ")";
            Logger.Log($"{prefix}.ctor()");
        }
        
        public ISpan CurrentSpan => CurrentSpanUtils.CurrentSpan;

        public IBinaryFormat BinaryFormat => new LoggingBinaryFormat();

        public ITextFormat TextFormat => new LoggingTextFormat();

        public ISpanBuilder SpanBuilder(string spanName)
        {
            Logger.Log($"{prefix}.SpanBuilder({spanName})");
            return new LoggingSpanBuilder(spanName, SpanKind.Internal);
        }

        public IDisposable WithSpan(ISpan span)
        {
            Logger.Log($"{prefix}.WithSpan");
            return new CurrentSpanUtils.LoggingScope(span);
        }
    }
}
