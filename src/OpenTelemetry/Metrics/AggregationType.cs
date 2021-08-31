using System;
using System.Collections.Generic;
using System.Text;

namespace OpenTelemetry.Metrics
{
    internal enum AggregationType
    {
        /// <summary>
        /// Invalid.
        /// </summary>
        Invalid = -1,

        /// <summary>
        /// Calculate SUM from incoming delta measurements.
        /// </summary>
        LongSumIncomingDelta = 0,

        /// <summary>
        /// Calculate SUM from incoming cumulative measurements.
        /// </summary>
        LongSumIncomingCumulative = 1,

        /// <summary>
        /// Calculate SUM from incoming delta measurements.
        /// </summary>
        DoubleSumIncomingDelta = 2,

        /// <summary>
        /// Calculate SUM from incoming cumulative measurements.
        /// </summary>
        DoubleSumIncomingCumulative = 3,

        /// <summary>
        /// Keep LastValue.
        /// </summary>
        LongGauge = 4,

        /// <summary>
        /// Keep LastValue.
        /// </summary>
        DoubleGauge = 5,

        /// <summary>
        /// Invalid.
        /// </summary>
        Histogram_Delta = 6,

        /// <summary>
        /// Calculate histogram.
        /// </summary>
        Histogram_Cumulative = 7,
    }
}
