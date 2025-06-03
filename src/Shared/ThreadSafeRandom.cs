// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

// Note: Inspired by https://devblogs.microsoft.com/pfxteam/getting-random-numbers-in-a-thread-safe-way/
internal static class ThreadSafeRandom
{
#if NET
    public static int Next(int min, int max)
    {
#pragma warning disable CA5394 // Do not use insecure randomness
        return Random.Shared.Next(min, max);
#pragma warning restore CA5394 // Do not use insecure randomness
    }
#else
    private static readonly Random GlobalRandom = new();

    [ThreadStatic]
    private static Random? localRandom;

    public static int Next(int min, int max)
    {
        var local = localRandom;
        if (local == null)
        {
            int seed;
            lock (GlobalRandom)
            {
#pragma warning disable CA5394 // Do not use insecure randomness
                seed = GlobalRandom.Next();
            }

            localRandom = local = new Random(seed);
        }

        return local.Next(min, max);
#pragma warning restore CA5394 // Do not use insecure randomness
    }
#endif
}
