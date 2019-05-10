// <copyright file="TimestampTest.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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

namespace OpenTelemetry.Common.Test
{
    using System;
    using Xunit;

    public class TimestampTest
    {
        [Fact]
        public void TimestampCreate()
        {
            Assert.Equal(24, Timestamp.Create(24, 42).Seconds);
            Assert.Equal(42, Timestamp.Create(24, 42).Nanos);
            Assert.Equal(-24, Timestamp.Create(-24, 42).Seconds);
            Assert.Equal(42, Timestamp.Create(-24, 42).Nanos);
            Assert.Equal(315576000000L, Timestamp.Create(315576000000L, 999999999).Seconds);
            Assert.Equal(999999999, Timestamp.Create(315576000000L, 999999999).Nanos);
            Assert.Equal(-315576000000L, Timestamp.Create(-315576000000L, 999999999).Seconds);
            Assert.Equal(999999999, Timestamp.Create(-315576000000L, 999999999).Nanos);
        }

        [Fact]
        public void TimestampCreate_InvalidInput()
        {
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.Create(-315576000001L, 0));
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.Create(315576000001L, 0));
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.Create(1, 1000000000));
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.Create(1, -1));
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.Create(-1, 1000000000));
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.Create(-1, -1));
        }

        [Fact]
        public void TimestampFromMillis()
        {
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.FromMillis(0));
            Assert.Equal(Timestamp.Create(0, 987000000), Timestamp.FromMillis(987));
            Assert.Equal(Timestamp.Create(3, 456000000), Timestamp.FromMillis(3456));
        }

        [Fact]
        public void TimestampFromMillis_Negative()
        {
            Assert.Equal(Timestamp.Create(-1, 999000000), Timestamp.FromMillis(-1));
            Assert.Equal(Timestamp.Create(-1, 1000000), Timestamp.FromMillis(-999));
            Assert.Equal(Timestamp.Create(-4, 544000000), Timestamp.FromMillis(-3456));
        }

        [Fact]
        public void TimestampAddNanos()
        {
            Timestamp timestamp = Timestamp.Create(1234, 223);
            Assert.Equal(timestamp, timestamp.AddNanos(0));
            Assert.Equal(Timestamp.Create(1235, 0), timestamp.AddNanos(999999777));
            Assert.Equal(Timestamp.Create(1235, 300200723), timestamp.AddNanos(1300200500));
            Assert.Equal(Timestamp.Create(1236, 0), timestamp.AddNanos(1999999777));
            Assert.Equal(Timestamp.Create(1243, 876544012), timestamp.AddNanos(9876543789L));
            Assert.Equal(Timestamp.Create(1234L + 9223372036L, 223 + 854775807), timestamp.AddNanos(Int64.MaxValue))
                ;
        }

        [Fact]
        public void TimestampAddNanos_Negative()
        {
            Timestamp timestamp = Timestamp.Create(1234, 223);
            Assert.Equal(Timestamp.Create(1234, 0), timestamp.AddNanos(-223));
            Assert.Equal(Timestamp.Create(1233, 0), timestamp.AddNanos(-1000000223));
            Assert.Equal(Timestamp.Create(1232, 699799723), timestamp.AddNanos(-1300200500));
            Assert.Equal(Timestamp.Create(1229, 876544010), timestamp.AddNanos(-4123456213L));
            Assert.Equal(Timestamp.Create(1234L - 9223372036L - 1, 223 + 145224192), timestamp.AddNanos(Int64.MinValue))
                ;
        }

        [Fact]
        public void TimestampAddDuration()
        {
            Timestamp timestamp = Timestamp.Create(1234, 223);
            Assert.Equal(Timestamp.Create(1235, 223), timestamp.AddDuration(Duration.Create(1, 0)));
            Assert.Equal(Timestamp.Create(1234, 224), timestamp.AddDuration(Duration.Create(0, 1)));
            Assert.Equal(Timestamp.Create(1235, 224), timestamp.AddDuration(Duration.Create(1, 1)));
            Assert.Equal(Timestamp.Create(1236, 123), timestamp.AddDuration(Duration.Create(1, 999999900)));
        }

        [Fact]
        public void TimestampAddDuration_Negative()
        {
            Timestamp timestamp = Timestamp.Create(1234, 223);
            Assert.Equal(Timestamp.Create(0, 0), timestamp.AddDuration(Duration.Create(-1234, -223)));
            Assert.Equal(Timestamp.Create(1233, 223), timestamp.AddDuration(Duration.Create(-1, 0)));
            Assert.Equal(Timestamp.Create(1233, 222), timestamp.AddDuration(Duration.Create(-1, -1)));
            Assert.Equal(Timestamp.Create(1232, 999999900), timestamp.AddDuration(Duration.Create(-1, -323)));
            Assert.Equal(Timestamp.Create(1200, 224), timestamp.AddDuration(Duration.Create(-33, -999999999)));
        }

        [Fact]
        public void TimestampSubtractTimestamp()
        {
            Timestamp timestamp = Timestamp.Create(1234, 223);
            Assert.Equal(Duration.Create(1234, 223), timestamp.SubtractTimestamp(Timestamp.Create(0, 0)));
            Assert.Equal(Duration.Create(1, 0), timestamp.SubtractTimestamp(Timestamp.Create(1233, 223)));
            Assert.Equal(Duration.Create(1, 1), timestamp.SubtractTimestamp(Timestamp.Create(1233, 222)));
            Assert.Equal(Duration.Create(1, 323), timestamp.SubtractTimestamp(Timestamp.Create(1232, 999999900)));
            Assert.Equal(Duration.Create(33, 999999999), timestamp.SubtractTimestamp(Timestamp.Create(1200, 224)));
        }

        [Fact]
        public void TimestampSubtractTimestamp_NegativeResult()
        {
            Timestamp timestamp = Timestamp.Create(1234, 223);
            Assert.Equal(Duration.Create(-1, 0), timestamp.SubtractTimestamp(Timestamp.Create(1235, 223)));
            Assert.Equal(Duration.Create(0, -1), timestamp.SubtractTimestamp(Timestamp.Create(1234, 224)));
            Assert.Equal(Duration.Create(-1, -1), timestamp.SubtractTimestamp(Timestamp.Create(1235, 224)));
            Assert.Equal(Duration.Create(-1, -999999900), timestamp.SubtractTimestamp(Timestamp.Create(1236, 123)));
        }

        [Fact]
        public void Timestamp_CompareTo()
        {
            Assert.Equal(0, Timestamp.Create(0, 0).CompareTo(Timestamp.Create(0, 0)));
            Assert.Equal(0, Timestamp.Create(24, 42).CompareTo(Timestamp.Create(24, 42)));
            Assert.Equal(0, Timestamp.Create(-24, 42).CompareTo(Timestamp.Create(-24, 42)));
            Assert.Equal(1, Timestamp.Create(25, 42).CompareTo(Timestamp.Create(24, 42)));
            Assert.Equal(1, Timestamp.Create(24, 45).CompareTo(Timestamp.Create(24, 42)));
            Assert.Equal(-1, Timestamp.Create(24, 42).CompareTo(Timestamp.Create(25, 42)));
            Assert.Equal(-1, Timestamp.Create(24, 42).CompareTo(Timestamp.Create(24, 45)));
            Assert.Equal(-1, Timestamp.Create(-25, 42).CompareTo(Timestamp.Create(-24, 42)));
            Assert.Equal(1, Timestamp.Create(-24, 45).CompareTo(Timestamp.Create(-24, 42)));
            Assert.Equal(1, Timestamp.Create(-24, 42).CompareTo(Timestamp.Create(-25, 42)));
            Assert.Equal(-1, Timestamp.Create(-24, 42).CompareTo(Timestamp.Create(-24, 45)));
        }

        [Fact]
        public void Timestamp_Equal()
        {
            // Positive tests.
            Assert.Equal(Timestamp.Create(0, 0), Timestamp.Create(0, 0));
            Assert.Equal(Timestamp.Create(24, 42), Timestamp.Create(24, 42));
            Assert.Equal(Timestamp.Create(-24, 42), Timestamp.Create(-24, 42));
            // Negative tests.
            Assert.NotEqual(Timestamp.Create(24, 42), Timestamp.Create(25, 42));
            Assert.NotEqual(Timestamp.Create(24, 42), Timestamp.Create(24, 43));
            Assert.NotEqual(Timestamp.Create(-24, 42), Timestamp.Create(-25, 42));
            Assert.NotEqual(Timestamp.Create(-24, 42), Timestamp.Create(-24, 43));
        }
    }
}
