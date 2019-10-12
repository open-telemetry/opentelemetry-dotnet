// <copyright file="LoggingTracer.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>
using System;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace LoggingTracer
{
    public class LoggingTracer : ITracer
    {
        private string prefix;

        internal LoggingTracer()
        {
            Logger.Log("Tracer.ctor()");
        }

        public ISpan CurrentSpan => CurrentSpanUtils.CurrentSpan;

        public IBinaryFormat BinaryFormat => new LoggingBinaryFormat();

        public ITextFormat TextFormat => new LoggingTextFormat();

        public IDisposable WithSpan(ISpan span)
        {
            Logger.Log($"{prefix}.WithSpan");
            return new CurrentSpanUtils.LoggingScope(span);
        }

        public ISpan StartRootSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            Logger.Log($"{prefix}.StartRootSpan({operationName}, {kind}, {startTimestamp:o}, {links})");
            return new LoggingSpan(operationName, kind);
        }

        public ISpan StartSpan(string operationName, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            Logger.Log($"{prefix}.StartSpan({operationName}, {kind}, {startTimestamp:o}, {links})");
            return new LoggingSpan(operationName, kind);
        }

        public ISpan StartSpan(string operationName, ISpan parent, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            Logger.Log($"{prefix}.StartSpan({operationName}, {parent.GetType().Name}, {kind} {startTimestamp:o}, {links})");
            return new LoggingSpan(operationName, kind);
        }

        public ISpan StartSpan(string operationName, in SpanContext parent, SpanKind kind, DateTimeOffset startTimestamp, IEnumerable<Link> links)
        {
            Logger.Log($"{prefix}.StartSpan({operationName}, {parent.GetType().Name}, {kind}, {startTimestamp:o}, {links})");
            return new LoggingSpan(operationName, kind);
        }


        public ISpan StartSpanFromActivity(string operationName, Activity activity, SpanKind kind, IEnumerable<Link> links)
        {
            Logger.Log($"{prefix}.StartSpanFromActivity({operationName}, {activity.OperationName}, {kind}, {links})");
            return new LoggingSpan(operationName, kind);
        }
    }
}
