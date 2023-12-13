// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Runtime.CompilerServices;

namespace OpenTelemetry.Tests.Stress;

public partial class Program
{
    public static void Main()
    {
        Stress(concurrency: 1, prometheusPort: 9464);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected static void Run()
    {
    }
}
