// <copyright file="MetricBehaviors.cs" company="OpenTelemetry Authors">
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

#pragma warning disable SA1649 // File name should match first type name

namespace OpenTelemetry.Metrics;

[Flags]
internal enum MetricBehaviors
{
#pragma warning disable SA1602 // Enumeration items should be documented
    None = 0,
    Long = 1,
    Double = 1 << 1,
    Sum = 1 << 2,
    Gauge = 1 << 3,
    Cumulative = 1 << 4,
    Delta = 1 << 5,
    Histogram = 1 << 6,
    HistogramRecordMinMax = 1 << 7,
    HistogramWithoutBuckets = 1 << 8,
    HistogramWithExponentialBuckets = 1 << 9,
    OfferExemplar = 1 << 10,
#pragma warning restore SA1602 // Enumeration items should be documented
}

internal interface IDeltaMetricBehavior
{
}

internal interface IHistogramWithoutBucketsMetricBehavior
{
}

internal interface IHistogramRecordMinMaxMetricBehavior
{
}

internal interface IHistogramWithExponentialBucketsMetricBehavior
{
}

internal interface IOfferExemplarMetricBehavior
{
}
