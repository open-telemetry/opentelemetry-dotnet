// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Internal;

internal class ThreadingHelper
{
    internal static bool IsThreadingDisabled()
    {
        // if the threadpool isn't using threads assume they aren't enabled
        ThreadPool.GetMaxThreads(out int workerThreads, out int completionPortThreads);

        return workerThreads == 1 && completionPortThreads == 1;
    }
}
