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
    Sum = 1,
    Gauge = 1 << 1,
    Cumulative = 1 << 2,
    Delta = 1 << 3,
    Histogram = 1 << 4,
    HistogramWithBuckets = 1 << 5,
    HistogramRecordMinMax = 1 << 6,
    ExponentialHistogram = 1 << 7,
    OfferExemplar = 1 << 8,
#pragma warning restore SA1602 // Enumeration items should be documented
}

internal interface IDeltaMetricBehavior
{
}

internal interface IHistogramBucketsMetricBehavior
{
}

internal interface IHistogramRecordMinMaxMetricBehavior
{
}

internal interface IExponentialHistogramMetricBehavior
{
}

internal interface IOfferExemplarMetricBehavior
{
}
