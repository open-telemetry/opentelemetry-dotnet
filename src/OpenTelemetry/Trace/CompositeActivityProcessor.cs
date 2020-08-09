// <copyright file="CompositeActivityProcessor.cs" company="OpenTelemetry Authors">
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
using System.Threading.Tasks;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Trace
{
    public class CompositeActivityProcessor : ActivityProcessor
    {
        private DoublyLinkedListNode<ActivityProcessor> head;
        private DoublyLinkedListNode<ActivityProcessor> tail;
        private bool disposed;

        public CompositeActivityProcessor(IEnumerable<ActivityProcessor> processors)
        {
            if (processors == null)
            {
                throw new ArgumentNullException(nameof(processors));
            }

            using var iter = processors.GetEnumerator();

            if (!iter.MoveNext())
            {
                throw new ArgumentException($"{nameof(processors)} collection is empty");
            }

            this.head = new DoublyLinkedListNode<ActivityProcessor>(iter.Current);
            this.tail = this.head;

            while (iter.MoveNext())
            {
                this.AddProcessor(iter.Current);
            }
        }

        public CompositeActivityProcessor AddProcessor(ActivityProcessor processor)
        {
            if (processor == null)
            {
                throw new ArgumentNullException(nameof(processor));
            }

            var node = new DoublyLinkedListNode<ActivityProcessor>(processor)
            {
                Previous = this.tail,
            };
            this.tail.Next = node;
            this.tail = node;

            return this;
        }

        public override void OnEnd(Activity activity)
        {
            var cur = this.head;

            while (cur != null)
            {
                cur.Value.OnEnd(activity);
                cur = cur.Next;
            }
        }

        public override void OnStart(Activity activity)
        {
            var cur = this.head;

            while (cur != null)
            {
                cur.Value.OnStart(activity);
                cur = cur.Next;
            }
        }

        public override Task ShutdownAsync(CancellationToken cancellationToken)
        {
            var cur = this.head;
            var task = cur.Value.ShutdownAsync(cancellationToken);

            for (cur = cur.Next; cur != null; cur = cur.Next)
            {
                var processor = cur.Value;
                task = task.ContinueWith(t => processor.ShutdownAsync(cancellationToken));
            }

            return task;
        }

        public override Task ForceFlushAsync(CancellationToken cancellationToken)
        {
            var cur = this.head;
            var task = cur.Value.ShutdownAsync(cancellationToken);

            for (cur = cur.Next; cur != null; cur = cur.Next)
            {
                var processor = cur.Value;
                task = task.ContinueWith(t => processor.ForceFlushAsync(cancellationToken));
            }

            return task;
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
                    catch (Exception ex)
                    {
                        OpenTelemetrySdkEventSource.Log.SpanProcessorException(nameof(this.Dispose), ex);
                    }

                    cur = cur.Next;
                }
            }

            this.disposed = true;
        }

        private class DoublyLinkedListNode<T>
        {
            public readonly T Value;

            public DoublyLinkedListNode(T value)
            {
                this.Value = value;
            }

            public DoublyLinkedListNode<T> Previous { get; set; }

            public DoublyLinkedListNode<T> Next { get; set; }
        }
    }
}
