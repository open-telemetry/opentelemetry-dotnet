// <copyright file="SampledTraceExemplarFilter.cs" company="OpenTelemetry Authors">
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

/// <summary>
/// The Exemplar Reservoir which has a fixed size buffer
/// implementing naive reservoir algorithm to store exemplars.
/// </summary>
internal sealed class SimpleFixedSizeExemplarReservoir : ExemplarReservoir
{
    private Exemplar[] runningExemplars;
    private Exemplar[] snapshotExemplars;

    private long numberOfMeasurementsSeen;
    private int size;

    private object lockObject = new object();
    private Random random = new Random();

    public SimpleFixedSizeExemplarReservoir(int size = 10)
    {
        this.size = size;
        this.runningExemplars = new Exemplar[size];
        this.snapshotExemplars = new Exemplar[size];
    }

    public override void Offer(long value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        // TODO: Replace simple lock with alternates.
        // TODO: Also, the updation of MP itself
        // can be moved inside this.
        lock (this.lockObject)
        {
            if (this.numberOfMeasurementsSeen < this.size)
            {
                var exemplar = default(Exemplar);
                exemplar.Timestamp = DateTime.UtcNow;
                exemplar.LongValue = value;
                exemplar.TraceId = Activity.Current?.TraceId;
                exemplar.SpanId = Activity.Current?.SpanId;
                this.runningExemplars[this.numberOfMeasurementsSeen] = exemplar;
                this.numberOfMeasurementsSeen++;
            }
            else
            {
                this.numberOfMeasurementsSeen++;
                var bucket = this.random.Next((int)this.numberOfMeasurementsSeen);
                if (bucket < this.size)
                {
                    var exemplar = default(Exemplar);
                    exemplar.Timestamp = DateTime.UtcNow;
                    exemplar.LongValue = value;
                    exemplar.TraceId = Activity.Current?.TraceId;
                    exemplar.SpanId = Activity.Current?.SpanId;
                    this.runningExemplars[bucket] = exemplar;
                }
            }
        }
    }

    public override void Offer(double value, ReadOnlySpan<KeyValuePair<string, object>> tags)
    {
        lock (this.lockObject)
        {
            if (this.numberOfMeasurementsSeen < this.size)
            {
                var exemplar = default(Exemplar);
                exemplar.Timestamp = DateTime.UtcNow;
                exemplar.DoubleValue = value;
                exemplar.TraceId = (ActivityTraceId)Activity.Current?.TraceId;
                exemplar.SpanId = (ActivitySpanId)Activity.Current?.SpanId;
                this.runningExemplars[this.numberOfMeasurementsSeen] = exemplar;
                this.numberOfMeasurementsSeen++;
            }
            else
            {
                this.numberOfMeasurementsSeen++;
                var bucket = this.random.Next((int)this.numberOfMeasurementsSeen);
                if (bucket < this.size)
                {
                    var exemplar = default(Exemplar);
                    exemplar.Timestamp = DateTime.UtcNow;
                    exemplar.DoubleValue = value;
                    exemplar.TraceId = (ActivityTraceId)Activity.Current?.TraceId;
                    exemplar.SpanId = (ActivitySpanId)Activity.Current?.SpanId;
                    this.runningExemplars[bucket] = exemplar;
                }
            }
        }
    }

    public override Exemplar[] Collect()
    {
        return this.snapshotExemplars;
    }

    public override void SnapShot()
    {
        lock (this.lockObject)
        {
            for (int i = 0; i < this.size; i++)
            {
                this.snapshotExemplars[i] = this.runningExemplars[i];
                this.runningExemplars[i].Timestamp = default;
            }
        }
    }
}
