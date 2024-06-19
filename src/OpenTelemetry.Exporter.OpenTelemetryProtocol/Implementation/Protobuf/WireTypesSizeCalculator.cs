// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Exporter.OpenTelemetryProtocol.Implementation.Protobuf;

internal class WireTypesSizeCalculator
{
    public static int ComputeTagSize(int fieldNumber)
    {
        return ComputeVarint32Size(MakeTag(fieldNumber, 0));
    }

    public static uint MakeTag(int fieldNumber, WireType wireType)
    {
        return (uint)(fieldNumber << 3) | (uint)wireType;
    }

    public static int ComputeLengthSize(int length)
    {
        return ComputeVarint32Size((uint)length);
    }

    public static int ComputeVarint32Size(uint value)
    {
        if ((value & (0xffffffff << 7)) == 0)
        {
            return 1;
        }

        if ((value & (0xffffffff << 14)) == 0)
        {
            return 2;
        }

        if ((value & (0xffffffff << 21)) == 0)
        {
            return 3;
        }

        if ((value & (0xffffffff << 28)) == 0)
        {
            return 4;
        }

        return 5;
    }

    public static int ComputeVarint64Size(ulong value)
    {
        if ((value & (0xffffffffffffffffL << 7)) == 0)
        {
            return 1;
        }

        if ((value & (0xffffffffffffffffL << 14)) == 0)
        {
            return 2;
        }

        if ((value & (0xffffffffffffffffL << 21)) == 0)
        {
            return 3;
        }

        if ((value & (0xffffffffffffffffL << 28)) == 0)
        {
            return 4;
        }

        if ((value & (0xffffffffffffffffL << 35)) == 0)
        {
            return 5;
        }

        if ((value & (0xffffffffffffffffL << 42)) == 0)
        {
            return 6;
        }

        if ((value & (0xffffffffffffffffL << 49)) == 0)
        {
            return 7;
        }

        if ((value & (0xffffffffffffffffL << 56)) == 0)
        {
            return 8;
        }

        if ((value & (0xffffffffffffffffL << 63)) == 0)
        {
            return 9;
        }

        return 10;
    }
}
