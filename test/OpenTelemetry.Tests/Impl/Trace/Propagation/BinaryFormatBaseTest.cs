﻿// <copyright file="BinaryFormatBaseTest.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Trace.Propagation.Test
{
    using System;
    using Xunit;

    public class BinaryFormatBaseTest
    {
        private static readonly IBinaryFormat binaryFormat = BinaryFormatBase.NoopBinaryFormat;

        [Fact]
        public void ToByteArray_NullSpanContext()
        {
            Assert.Throws<ArgumentNullException>(() => binaryFormat.ToByteArray(null));
        }

        [Fact]
        public void ToByteArray_NotNullSpanContext()
        {
            Assert.Equal(new byte[0], binaryFormat.ToByteArray(SpanContext.Invalid));
        }

        [Fact]
        public void FromByteArray_NullInput()
        {
            Assert.Throws<ArgumentNullException>(() => binaryFormat.FromByteArray(null));
        }

        [Fact]
        public void FromByteArray_NotNullInput()
        {
            Assert.Equal(SpanContext.Invalid, binaryFormat.FromByteArray(new byte[0]));
        }
    }
}

