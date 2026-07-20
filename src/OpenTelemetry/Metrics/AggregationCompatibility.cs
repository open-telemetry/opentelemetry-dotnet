// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0
namespace OpenTelemetry.Metrics;

/// <summary>
/// Isolates the compatibility matrix between <see cref="AggregationType"/> and
/// System.Diagnostics.Metrics instrument kinds.
///
/// Modeled on OTel Go's `isAggregatorCompatible` (sdk/metric/pipeline.go),
/// for .NET's AggregationType enum, which (unlike Go's Aggregation interface)
/// already bakes numeric width into the type name (Long*/Double*), and
/// .NET's Gauge synchronous instrument kind, which Go's SDK does not have.
///
/// This type is NOT referenced by Metric.cs or MeterProviderSdk.cs.
///
///
/// (i) = integral value type only (long/int/short/byte)
/// (f) = floating value type only (double/float)
/// See the OpenTelemetry metrics SDK specification for details:
/// https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/metrics/sdk.md#aggregation
///
/// Counter / UpDownCounter / Histogram:
///
/// | AggregationType          | Counter | UpDownCounter | Histogram |
/// |--------------------------|:-------:|:-------------:|:---------:|
/// | LongSumIncomingDelta     | Y (i)   |    Y (i)      |  Y (i)    |
/// | DoubleSumIncomingDelta   | Y (f)   |    Y (f)      |  Y (f)    |
/// | Histogram and variants   |   Y     |      Y        |    Y      |
///
/// ObservableCounter / ObservableUpDownCounter:
///
/// | AggregationType           | ObservableCounter | ObservableUpDownCounter |
/// |---------------------------|:-----------------:|:-----------------------:|
/// | LongSumIncomingCumulative |      Y (i)        |         Y (i)           |
/// | DoubleSumIncomingCumulative|      Y (f)       |         Y (f)           |
/// | Histogram and variants[1] |        Y          |           Y             |
///
/// Gauge / ObservableGauge:
///
/// | AggregationType | Gauge | ObservableGauge |
/// |-----------------|:-----:|:---------------:|
/// | LongGauge       | Y (i) |     Y (i)       |
/// | DoubleGauge     | Y (f) |     Y (f)       |
///
/// </summary>
internal static class AggregationCompatibility
{
    /// <summary>
    /// Determines whether <paramref name="aggregationType"/> may be used to
    /// aggregate measurements from an instrument of the given CLR
    /// <paramref name="instrumentType"/> (<c>typeof(Counter)</c>).
    /// </summary>
    /// <param name="aggregationType">The candidate aggregation.</param>
    /// <param name="instrumentType">
    /// The closed generic instrument type, e.g.
    /// <c>typeof(Counter)</c> or
    /// <c>typeof(ObservableGauge)</c>. Note:
    /// MetricStreamConfiguration.Drop is a view-level construct, not
    /// an AggregationType, and is out of scope here. allers should
    /// short-circuit on Drop before reaching this method.
    /// </param>
    /// <returns>
    /// <see langword="true"/> if compatible; <see langword="false"/>
    /// if the instrument kind or numeric type is not accepted, or if
    /// <paramref name="instrumentType"/> is not a recognized
    /// System.Diagnostics.Metrics instrument type.
    /// </returns>
    public static bool IsCompatible(AggregationType aggregationType, Type instrumentType)
    {
        ArgumentNullException.ThrowIfNull(instrumentType);

        if (!InstrumentTypeInspector.TryClassify(instrumentType, out var kind, out var numeric))
        {
            return false;
        }

        if (kind == InstrumentKind.Counter || kind == InstrumentKind.UpDownCounter)
        {
            switch (aggregationType)
            {
                case AggregationType.LongSumIncomingDelta:
                    return numeric == NumericKind.Integral;
                case AggregationType.DoubleSumIncomingDelta:
                    return numeric == NumericKind.Floating;
                case AggregationType.Histogram:
                case AggregationType.HistogramWithBuckets:
                case AggregationType.HistogramWithMinMax:
                case AggregationType.HistogramWithMinMaxBuckets:
                case AggregationType.Base2ExponentialHistogram:
                case AggregationType.Base2ExponentialHistogramWithMinMax:
                    return true;
                default:
                    return false;
            }
        }

        if (kind == InstrumentKind.Histogram)
        {
            switch (aggregationType)
            {
                case AggregationType.LongSumIncomingDelta:
                    return numeric == NumericKind.Integral;
                case AggregationType.DoubleSumIncomingDelta:
                    return numeric == NumericKind.Floating;
                case AggregationType.Histogram:
                case AggregationType.HistogramWithBuckets:
                case AggregationType.HistogramWithMinMax:
                case AggregationType.HistogramWithMinMaxBuckets:
                case AggregationType.Base2ExponentialHistogram:
                case AggregationType.Base2ExponentialHistogramWithMinMax:
                    return true;
                default:
                    return false;
            }
        }

        if (kind == InstrumentKind.ObservableCounter || kind == InstrumentKind.ObservableUpDownCounter)
        {
            switch (aggregationType)
            {
                case AggregationType.LongSumIncomingCumulative:
                    return numeric == NumericKind.Integral;
                case AggregationType.DoubleSumIncomingCumulative:
                    return numeric == NumericKind.Floating;
                case AggregationType.Histogram:
                case AggregationType.HistogramWithBuckets:
                case AggregationType.HistogramWithMinMax:
                case AggregationType.HistogramWithMinMaxBuckets:
                case AggregationType.Base2ExponentialHistogram:
                case AggregationType.Base2ExponentialHistogramWithMinMax:
                    return true;
                default:
                    return false;
            }
        }

        if (kind == InstrumentKind.Gauge || kind == InstrumentKind.ObservableGauge)
        {
            switch (aggregationType)
            {
                case AggregationType.LongGauge:
                    return numeric == NumericKind.Integral;
                case AggregationType.DoubleGauge:
                    return numeric == NumericKind.Floating;
                default:
                    return false;
            }
        }

        throw new NotSupportedException(
            $"InstrumentKind '{kind}' is not recognized by {nameof(AggregationCompatibility)}. " +
            "This table must be updated whenever a new InstrumentKind is added.");
    }
}