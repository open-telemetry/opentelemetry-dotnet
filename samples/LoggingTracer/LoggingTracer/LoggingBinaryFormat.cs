// <copyright file="LoggingBinaryFormat.cs" company="PlaceholderCompany">
// Copyright (c) PlaceholderCompany. All rights reserved.
// </copyright>

namespace LoggingTracer
{
    using System;
    using System.Collections.Generic;
    using OpenTelemetry.Context.Propagation;
    using OpenTelemetry.Trace;

    public sealed class LoggingBinaryFormat : IBinaryFormat
    {
        public ISet<string> Fields => null;

        public SpanContext Extract<T>(T carrier, Func<T, string, IEnumerable<string>> getter)
        {
            Logger.Log("LoggingBinaryFormat.Extract(...)");
            return SpanContext.Blank;
        }

        public SpanContext FromByteArray(byte[] bytes)
        {
            Logger.Log("LoggingBinaryFormat.FromByteArray(...)");
            return SpanContext.Blank;
        }

        public void Inject<T>(SpanContext spanContext, T carrier, Action<T, string, string> setter)
            => Logger.Log($"LoggingBinaryFormat.Inject({spanContext}, ...)");

        public byte[] ToByteArray(SpanContext spanContext)
        {
            Logger.Log($"LoggingBinaryFormat.ToByteArray({spanContext})");
            return Array.Empty<byte>();
        }
    }
}
