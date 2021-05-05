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

            if (activity.Kind == ActivityKind.Internal)
            {
                // TODO: span
                return default;
            }

            // TODO: transaction
            return default;
        }
    }
}
