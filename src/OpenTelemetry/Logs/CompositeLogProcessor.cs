// <copyright file="CompositeLogProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs
{
    internal sealed class CompositeLogProcessor : ILogProcessor
    {
        private readonly DoublyLinkedListNode head;
        private DoublyLinkedListNode tail;
        private bool disposed;

        public CompositeLogProcessor(IEnumerable<ILogProcessor> processors)
        {
            Guard.ThrowIfNull(processors);

            using var iter = processors.GetEnumerator();
            if (!iter.MoveNext())
            {
                throw new ArgumentException($"'{processors}' is null or empty", nameof(processors));
            }

            this.head = new DoublyLinkedListNode(iter.Current);
            this.tail = this.head;

            while (iter.MoveNext())
            {
                this.AddProcessor(iter.Current);
            }
        }

        public CompositeLogProcessor AddProcessor(ILogProcessor processor)
        {
            Guard.ThrowIfNull(processor);

            var node = new DoublyLinkedListNode(processor)
            {
                Previous = this.tail,
            };
            this.tail.Next = node;
            this.tail = node;

            return this;
        }

        public void OnEnd(in LogRecordStruct log)
        {
            for (var cur = this.head; cur != null; cur = cur.Next)
            {
                cur.Value.OnEnd(in log);
            }
        }

        public bool ForceFlush(int timeoutMilliseconds = -1)
        {
            var result = true;
            var sw = timeoutMilliseconds == Timeout.Infinite
                ? null
                : Stopwatch.StartNew();

            for (var cur = this.head; cur != null; cur = cur.Next)
            {
                if (sw == null)
                {
                    result = cur.Value.ForceFlush(Timeout.Infinite) && result;
                }
                else
                {
                    var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                    // notify all the processors, even if we run overtime
                    result = cur.Value.ForceFlush((int)Math.Max(timeout, 0)) && result;
                }
            }

            return result;
        }

        public bool Shutdown(int timeoutMilliseconds = -1)
        {
            var result = true;
            var sw = timeoutMilliseconds == Timeout.Infinite
                ? null
                : Stopwatch.StartNew();

            for (var cur = this.head; cur != null; cur = cur.Next)
            {
                if (sw == null)
                {
                    result = cur.Value.Shutdown(Timeout.Infinite) && result;
                }
                else
                {
                    var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                    // notify all the processors, even if we run overtime
                    result = cur.Value.Shutdown((int)Math.Max(timeout, 0)) && result;
                }
            }

            return result;
        }

        public void Dispose()
        {
            if (!this.disposed)
            {
                for (var cur = this.head; cur != null; cur = cur.Next)
                {
                    try
                    {
                        cur.Value?.Dispose();
                    }
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                    }
                }

                this.disposed = true;
            }
        }

        void ILogProcessor.SetParentProvider(BaseProvider parentProvider)
        {
        }

        private class DoublyLinkedListNode
        {
            public readonly ILogProcessor Value;

            public DoublyLinkedListNode(ILogProcessor value)
            {
                this.Value = value;
            }

            public DoublyLinkedListNode Previous { get; set; }

            public DoublyLinkedListNode Next { get; set; }
        }
    }
}
