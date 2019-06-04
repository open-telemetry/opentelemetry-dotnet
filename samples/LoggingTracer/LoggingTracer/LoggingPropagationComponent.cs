using System;
using System.Collections.Generic;
using OpenTelemetry.Trace;
using OpenTelemetry.Trace.Propagation;

namespace LoggingTracer
{
    public sealed class LoggingPropagationComponent : IPropagationComponent
    {
        private readonly IBinaryFormat binaryFormat = null; // TODO
        private readonly ITextFormat textFormat = new LoggingTextFormat();

        /// <inheritdoc/>
        public IBinaryFormat BinaryFormat
        {
            get
            {
                return binaryFormat;
            }
        }

        /// <inheritdoc/>
        public ITextFormat TextFormat
        {
            get
            {
                return textFormat;
            }
        }
    }

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


