// <copyright file="BatchTest.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry.Internal;
using Xunit;

namespace OpenTelemetry.Trace.Tests
{
    public class BatchTest
    {
        [Fact]
        public void CheckConstructorExceptions()
        {
            Assert.Throws<ArgumentNullException>(() => new Batch<string>(null));
            Assert.Throws<ArgumentNullException>(() => new Batch<string>(null, 1));
        }

        [Fact]
        public void CheckValidConstructors()
        {
            var value = "a";
            var batch = new Batch<string>(value);
            foreach (var item in batch)
            {
                Assert.Equal(value, item);
            }

            var circularBuffer = new CircularBuffer<string>(1);
            circularBuffer.Add(value);
            batch = new Batch<string>(circularBuffer, 1);
            foreach (var item in batch)
            {
                Assert.Equal(value, item);
            }
        }

        [Fact]
        public void CheckEnumerator()
        {
            var value = "a";
            var batch = new Batch<string>(value);
            var enumerator = batch.GetEnumerator();
            this.ValidateEnumerator(enumerator, value);

            var circularBuffer = new CircularBuffer<string>(1);
            circularBuffer.Add(value);
            batch = new Batch<string>(circularBuffer, 1);
            enumerator = batch.GetEnumerator();
            this.ValidateEnumerator(enumerator, value);
        }

        [Fact]
        public void CheckEnumeratorResetException()
        {
            var value = "a";
            var batch = new Batch<string>(value);
            var enumerator = batch.GetEnumerator();
            Assert.Throws<NotSupportedException>(() => enumerator.Reset());
        }

        private void ValidateEnumerator(Batch<string>.Enumerator enumerator, string expected)
        {
            if (enumerator.Current != null)
            {
                Assert.Equal(expected, enumerator.Current);
            }

            if (enumerator.MoveNext())
            {
                Assert.Equal(expected, enumerator.Current);
            }
        }
    }
}
