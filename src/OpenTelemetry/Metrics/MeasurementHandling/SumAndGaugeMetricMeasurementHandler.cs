// <copyright file="SumAndGaugeMetricMeasurementHandler.cs" company="OpenTelemetry Authors">
// Copyright The OpenTelemetry Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//     http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// </copyright>

using System.Diagnostics;

namespace OpenTelemetry.Metrics;

internal sealed class SumAndGaugeMetricMeasurementHandler<T> : MetricMeasurementHandler
    where T : struct // <- Note: T must be a struct to have specialized code generated based on the type
{
    private static readonly bool IsDelta = typeof(IDeltaMetricBehavior).IsAssignableFrom(typeof(T));
    private static readonly bool OfferExemplar = typeof(IOfferExemplarMetricBehavior).IsAssignableFrom(typeof(T));

    public override void RecordMeasurement(
        ref MetricPoint metricPoint,
        long value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled)
    {
        if (OfferExemplar)
        {
            Debug.Assert(metricPoint.OptionalComponents != null, "mpComponents was null");

            var optionalComponents = metricPoint.OptionalComponents!;

            optionalComponents.AcquireLock();

            if (IsDelta)
            {
                unchecked
                {
                    metricPoint.RunningValue.AsLong += value;
                }
            }
            else
            {
                // Note: Cumulative sum and gauge both fall here.
                metricPoint.RunningValue.AsLong = value;
            }

            if (isSampled)
            {
                Debug.Assert(metricPoint.OptionalComponents!.ExemplarReservoir != null, "ExemplarReservoir was null");

                // TODO: Need to ensure that the lock is always released.
                // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                optionalComponents.ExemplarReservoir!.Offer(value, tags);
            }

            optionalComponents.ReleaseLock();
        }
        else if (IsDelta)
        {
            Interlocked.Add(ref metricPoint.RunningValue.AsLong, value);
        }
        else
        {
            // Note: Cumulative sum and gauge both fall here.
            Interlocked.Exchange(ref metricPoint.RunningValue.AsLong, value);
        }
    }

    public override void RecordMeasurement(
        ref MetricPoint metricPoint,
        double value,
        ReadOnlySpan<KeyValuePair<string, object?>> tags,
        bool isSampled)
    {
        if (OfferExemplar)
        {
            Debug.Assert(metricPoint.OptionalComponents != null, "mpComponents was null");

            var optionalComponents = metricPoint.OptionalComponents!;

            optionalComponents.AcquireLock();

            if (IsDelta)
            {
                unchecked
                {
                    metricPoint.RunningValue.AsDouble += value;
                }
            }
            else
            {
                // Note: Cumulative sum and gauge both fall here.
                metricPoint.RunningValue.AsDouble = value;
            }

            if (isSampled)
            {
                Debug.Assert(metricPoint.OptionalComponents!.ExemplarReservoir != null, "ExemplarReservoir was null");

                // TODO: Need to ensure that the lock is always released.
                // A custom implementation of `ExemplarReservoir.Offer` might throw an exception.
                optionalComponents.ExemplarReservoir!.Offer(value, tags);
            }

            optionalComponents.ReleaseLock();
        }
        else if (IsDelta)
        {
            double initValue, newValue;
            var sw = default(SpinWait);
            while (true)
            {
                initValue = metricPoint.RunningValue.AsDouble;

                unchecked
                {
                    newValue = initValue + value;
                }

                if (initValue == Interlocked.CompareExchange(ref metricPoint.RunningValue.AsDouble, newValue, initValue))
                {
                    break;
                }

                sw.SpinOnce();
            }
        }
        else
        {
            // Note: Cumulative sum and gauge both fall here.
            Interlocked.Exchange(ref metricPoint.RunningValue.AsDouble, value);
        }
    }
}
