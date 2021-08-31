using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTelemetry.Metrics
{
    public enum AggregationTemporality
    {
        /// <summary>
        /// Cumulative.
        /// </summary>
        Cumulative = 1,

        /// <summary>
        /// Delta.
        /// </summary>
        Delta = 2,
    }
}
