// <copyright file="CompositeMetricReader.cs" company="OpenTelemetry Authors">
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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Metrics
{
    public class CompositeMetricReader : MetricReader
    {
        private readonly DoublyLinkedListNode head;
        private DoublyLinkedListNode tail;
        private bool disposed;

        public CompositeMetricReader(IEnumerable<MetricReader> readers)
        {
            if (readers == null)
            {
                throw new ArgumentNullException(nameof(readers));
            }

            using var iter = readers.GetEnumerator();

            if (!iter.MoveNext())
            {
                throw new ArgumentException($"{nameof(readers)} collection is empty");
            }

            this.head = new DoublyLinkedListNode(iter.Current);
            this.tail = this.head;

            while (iter.MoveNext())
            {
                this.AddReader(iter.Current);
            }
        }

        public CompositeMetricReader AddReader(MetricReader reader)
        {
            if (reader == null)
            {
                throw new ArgumentNullException(nameof(reader));
            }

            var node = new DoublyLinkedListNode(reader)
            {
                Previous = this.tail,
            };
            this.tail.Next = node;
            this.tail = node;

            return this;
        }

        /// <inheritdoc/>
        public override void OnCollect(Batch<Metric> metrics)
        {
            var cur = this.head;

            while (cur != null)
            {
                cur.Value.OnCollect(metrics);
                cur = cur.Next;
            }
        }

        /// <inheritdoc/>
        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            var cur = this.head;

            var sw = Stopwatch.StartNew();

            while (cur != null)
            {
                if (timeoutMilliseconds == Timeout.Infinite)
                {
                    _ = cur.Value.ForceFlush(Timeout.Infinite);
                }
                else
                {
                    var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                    if (timeout <= 0)
                    {
                        return false;
                    }

                    var succeeded = cur.Value.ForceFlush((int)timeout);

                    if (!succeeded)
                    {
                        return false;
                    }
                }

                cur = cur.Next;
            }

            return true;
        }

        /// <inheritdoc/>
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            var cur = this.head;
            var result = true;
            var sw = Stopwatch.StartNew();

            while (cur != null)
            {
                if (timeoutMilliseconds == Timeout.Infinite)
                {
                    result = cur.Value.Shutdown(Timeout.Infinite) && result;
                }
                else
                {
                    var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                    // notify all the processors, even if we run overtime
                    result = cur.Value.Shutdown((int)Math.Max(timeout, 0)) && result;
                }

                cur = cur.Next;
            }

            return result;
        }

        protected override void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }

            if (disposing)
            {
                var cur = this.head;

                while (cur != null)
                {
                    try
                    {
                        cur.Value?.Dispose();
                    }
                    catch (Exception)
                    {
                        // TODO: which event source do we use?
                        // OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                    }

                    cur = cur.Next;
                }
            }

            this.disposed = true;
        }

        private class DoublyLinkedListNode
        {
            public readonly MetricReader Value;

            public DoublyLinkedListNode(MetricReader value)
            {
                this.Value = value;
            }

            public DoublyLinkedListNode Previous { get; set; }

            public DoublyLinkedListNode Next { get; set; }
        }
    }
}
