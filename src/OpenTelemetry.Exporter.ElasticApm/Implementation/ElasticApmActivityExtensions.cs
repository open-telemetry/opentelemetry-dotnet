using System;
using System.Diagnostics;

namespace OpenTelemetry.Exporter.ElasticApm.Implementation
{
    internal static class ElasticApmActivityExtensions
    {
        internal static IElasticApmSpan ToElasticApmSpan(
            this Activity activity,
            IntakeApiVersion intakeApiVersion)
        {
            if (intakeApiVersion != IntakeApiVersion.V2)
            {
                throw new NotSupportedException();
            }

            // TODO: all strings "maxLength": 1024

            string name = activity.DisplayName;
            string traceId = activity.GetTraceId();
            string id = activity.GetSpanId();
            string parentId = activity.GetParentId();
            long duration = activity.Duration.ToEpochMicroseconds();
            long timestamp = activity.StartTimeUtc.ToEpochMicroseconds();
            string type = activity.GetActivityType();

            if (activity.Kind == ActivityKind.Internal)
            {
                return new V2.ElasticApmSpan("foo");
            }

            return new V2.ElasticApmTransaction(
                name,
                traceId,
                id,
                parentId,
                duration,
                timestamp,
                type);
        }

        private static string GetSpanId(this Activity activity)
        {
            return activity.SpanId.ToHexString();
        }

        private static string GetTraceId(this Activity activity)
        {
            return activity.Context.TraceId.ToHexString();
        }

        private static string GetParentId(this Activity activity)
        {
            return activity.ParentSpanId == default
                ? null
                : activity.ParentSpanId.ToHexString();
        }

        private static string GetActivityType(this Activity activity)
        {
            return activity.Kind switch
            {
                ActivityKind.Server => "server",
                ActivityKind.Producer => "producer",
                ActivityKind.Consumer => "consumer",
                ActivityKind.Client => "client",
                _ => null,
            };
        }
    }
}
