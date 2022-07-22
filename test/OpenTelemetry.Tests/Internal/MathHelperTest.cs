// <copyright file="MathHelperTest.cs" company="OpenTelemetry Authors">
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

using Xunit;

namespace OpenTelemetry.Internal.Tests;

public class MathHelperTest
{
    [Theory]
    [InlineData(0b0000_0000, 8)]
    [InlineData(0b0000_0001, 7)]
    [InlineData(0b0000_0010, 6)]
    [InlineData(0b0000_0011, 6)]
    [InlineData(0b0000_0100, 5)]
    [InlineData(0b0000_0101, 5)]
    [InlineData(0b0000_0111, 5)]
    [InlineData(0b0000_1000, 4)]
    [InlineData(0b0000_1001, 4)]
    [InlineData(0b0000_1111, 4)]
    [InlineData(0b0001_0000, 3)]
    [InlineData(0b0001_0001, 3)]
    [InlineData(0b0001_1111, 3)]
    [InlineData(0b0010_0000, 2)]
    [InlineData(0b0010_0001, 2)]
    [InlineData(0b0011_1111, 2)]
    [InlineData(0b0100_0000, 1)]
    [InlineData(0b0100_0001, 1)]
    [InlineData(0b0111_1111, 1)]
    [InlineData(0b1000_0000, 0)]
    [InlineData(0b1000_0001, 0)]
    [InlineData(0b1111_1111, 0)]
    public void LeadingZero8(byte value, int numberOfLeaderZeros)
    {
        Assert.Equal(numberOfLeaderZeros, MathHelper.LeadingZero8(value));
    }

    [Theory]
    [InlineData(unchecked((short)0b0000_0000_0000_0000), 16)]
    [InlineData(unchecked((short)0b0000_0000_0000_0001), 15)]
    [InlineData(unchecked((short)0b0000_0000_1000_0000), 8)]
    [InlineData(unchecked((short)0b0000_0001_0000_0000), 7)]
    [InlineData(unchecked((short)0b1000_0000_0000_0000), 0)]
    public void LeadingZero16(short value, int numberOfLeaderZeros)
    {
        Assert.Equal(numberOfLeaderZeros, MathHelper.LeadingZero16(value));
    }

    [Theory]
    [InlineData(0b0000_0000_0000_0000_0000_0000_0000_0000, 32)]
    [InlineData(0b0000_0000_0000_0000_0000_0000_0000_0001, 31)]
    [InlineData(0b0000_0000_0000_0000_1000_0000_0000_0000, 16)]
    [InlineData(0b0000_0000_0000_0001_0000_0000_0000_0000, 15)]
    [InlineData(unchecked((int)0b1000_0000_0000_0000_0000_0000_0000_0000), 0)]
    public void LeadingZero32(int value, int numberOfLeaderZeros)
    {
        Assert.Equal(numberOfLeaderZeros, MathHelper.LeadingZero32(value));
    }

    [Theory]
    [InlineData(0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000L, 64)]
    [InlineData(0b0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0001L, 63)]
    [InlineData(0b0000_0000_0000_0000_0000_0000_0000_0000_1000_0000_0000_0000_0000_0000_0000_0000L, 32)]
    [InlineData(0b0000_0000_0000_0000_0000_0000_0000_0001_0000_0000_0000_0000_0000_0000_0000_0000L, 31)]
    [InlineData(unchecked((long)0b1000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000_0000), 0)]
    public void LeadingZero64(long value, int numberOfLeaderZeros)
    {
        Assert.Equal(numberOfLeaderZeros, MathHelper.LeadingZero64(value));
    }
}
