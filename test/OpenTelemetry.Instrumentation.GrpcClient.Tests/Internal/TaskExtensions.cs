// <copyright file="TaskExtensions.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace OpenTelemetry.Instrumentation.GrpcClient.Internal.Tests
{
    internal static class TaskExtensions
    {
        public static async Task<T> TimeoutAfter<T>(this Task<T> task, TimeSpan timeout)
        {
            // Don't create a timer if the task is already completed
            // or the debugger is attached
            if (task.IsCompleted || Debugger.IsAttached)
            {
                return await task;
            }

            var cts = new CancellationTokenSource();
            if (task == await Task.WhenAny(task, Task.Delay(timeout, cts.Token)))
            {
                cts.Cancel();
                return await task;
            }
            else
            {
                throw new TimeoutException($"The operation timed out after reaching the limit of {timeout.TotalMilliseconds}ms.");
            }
        }
    }
}
