// <copyright file="CompositeProcessor.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry
{
    public class CompositeProcessor<T> : BaseProcessor<T>
    {
        private readonly DoublyLinkedListNode head;
        private DoublyLinkedListNode tail;
        private bool disposed;

        public CompositeProcessor(IEnumerable<BaseProcessor<T>> processors)
        {
            Guard.Null(processors, nameof(processors));

            using var iter = processors.GetEnumerator();
            if (!iter.MoveNext())
            {
                throw new ArgumentException($"'{iter}' is null or empty", nameof(iter));
            }

            this.head = new DoublyLinkedListNode(iter.Current);
            this.tail = this.head;

            while (iter.MoveNext())
            {
                this.AddProcessor(iter.Current);
            }
        }

        public CompositeProcessor<T> AddProcessor(BaseProcessor<T> processor)
        {
            Guard.Null(processor, nameof(processor));

            var node = new DoublyLinkedListNode(processor)
            {
                Previous = this.tail,
            };
            this.tail.Next = node;
            this.tail = node;

            return this;
        }

        /// <inheritdoc/>
        public override void OnEnd(T data)
        {
            var cur = this.head;

            while (cur != null)
            {
                cur.Value.OnEnd(data);
                cur = cur.Next;
            }
        }

        /// <inheritdoc/>
        public override void OnStart(T data)
        {
            var cur = this.head;

            while (cur != null)
            {
                cur.Value.OnStart(data);
                cur = cur.Next;
            }
        }

        /// <inheritdoc/>
        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            var result = true;
            var cur = this.head;
            var sw = Stopwatch.StartNew();

            while (cur != null)
            {
                if (timeoutMilliseconds == Timeout.Infinite)
                {
                    result = cur.Value.ForceFlush(Timeout.Infinite) && result;
                }
                else
                {
                    var timeout = timeoutMilliseconds - sw.ElapsedMilliseconds;

                    // notify all the processors, even if we run overtime
                    result = cur.Value.ForceFlush((int)Math.Max(timeout, 0)) && result;
                }

                cur = cur.Next;
            }

            return result;
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

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    var cur = this.head;

                    while (cur != null)
                    {
                        try
                        {
                            cur.Value?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                        }

                        cur = cur.Next;
                    }
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        private class DoublyLinkedListNode
        {
            public readonly BaseProcessor<T> Value;

            public DoublyLinkedListNode(BaseProcessor<T> value)
            {
                this.Value = value;
            }

            public DoublyLinkedListNode Previous { get; set; }

            public DoublyLinkedListNode Next { get; set; }
        }
    }
}
