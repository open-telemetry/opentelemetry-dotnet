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

#nullable enable

using System.Diagnostics;
using OpenTelemetry.Internal;

namespace OpenTelemetry
{
    public class CompositeProcessor<T> : BaseProcessor<T>
    {
        internal readonly DoublyLinkedListNode Head;
        private DoublyLinkedListNode tail;
        private bool disposed;

        public CompositeProcessor(IEnumerable<BaseProcessor<T>> processors)
        {
            Guard.ThrowIfNull(processors);

            using var iter = processors.GetEnumerator();
            if (!iter.MoveNext())
            {
                throw new ArgumentException($"'{iter}' is null or empty", nameof(iter));
            }

            this.Head = new DoublyLinkedListNode(iter.Current);
            this.tail = this.Head;

            while (iter.MoveNext())
            {
                this.AddProcessor(iter.Current);
            }
        }

        public CompositeProcessor<T> AddProcessor(BaseProcessor<T> processor)
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

        /// <inheritdoc/>
        public override void OnEnd(T data)
        {
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                cur.Value.OnEnd(data);
            }
        }

        /// <inheritdoc/>
        public override void OnStart(T data)
        {
            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                cur.Value.OnStart(data);
            }
        }

        internal override void SetParentProvider(BaseProvider parentProvider)
        {
            base.SetParentProvider(parentProvider);

            for (var cur = this.Head; cur != null; cur = cur.Next)
            {
                cur.Value.SetParentProvider(parentProvider);
            }
        }

        /// <inheritdoc/>
        protected override bool OnForceFlush(int timeoutMilliseconds)
        {
            var result = true;
            var sw = timeoutMilliseconds == Timeout.Infinite
                ? null
                : Stopwatch.StartNew();

            for (var cur = this.Head; cur != null; cur = cur.Next)
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

        /// <inheritdoc/>
        protected override bool OnShutdown(int timeoutMilliseconds)
        {
            var result = true;
            var sw = timeoutMilliseconds == Timeout.Infinite
                ? null
                : Stopwatch.StartNew();

            for (var cur = this.Head; cur != null; cur = cur.Next)
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

        /// <inheritdoc/>
        protected override void Dispose(bool disposing)
        {
            if (!this.disposed)
            {
                if (disposing)
                {
                    for (var cur = this.Head; cur != null; cur = cur.Next)
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
                }

                this.disposed = true;
            }

            base.Dispose(disposing);
        }

        internal sealed class DoublyLinkedListNode
        {
            public readonly BaseProcessor<T> Value;

            public DoublyLinkedListNode(BaseProcessor<T> value)
            {
                this.Value = value;
            }

            public DoublyLinkedListNode? Previous { get; set; }

            public DoublyLinkedListNode? Next { get; set; }
        }
    }
}
