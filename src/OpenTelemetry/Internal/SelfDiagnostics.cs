// <copyright file="SelfDiagnostics.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Internal
{
    /// <summary>
    /// Self diagnostics class captures the EventSource events sent by OpenTelemetry
    /// modules and writes them to local file for internal troubleshooting.
    /// </summary>
    internal class SelfDiagnostics : IDisposable
    {
        /// <summary>
        /// Long-living object that hold relevant resources.
        /// </summary>
        private static readonly SelfDiagnostics Instance = new();
        private readonly SelfDiagnosticsConfigRefresher configRefresher;

        static SelfDiagnostics()
        {
            AppDomain.CurrentDomain.ProcessExit += (sender, eventArgs) =>
            {
                Instance.Dispose();
            };
        }

        private SelfDiagnostics()
        {
            this.configRefresher = new SelfDiagnosticsConfigRefresher();
        }

        /// <summary>
        /// No member of SelfDiagnostics class is explicitly called when an EventSource class, say
        /// OpenTelemetryApiEventSource, is invoked to send an event.
        /// To trigger CLR to initialize static fields and static constructors of SelfDiagnostics,
        /// call EnsureInitialized method before any EventSource event is sent.
        /// </summary>
        public static void EnsureInitialized()
        {
        }

        /// <inheritdoc/>
        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.configRefresher.Dispose();
            }
        }
    }
}
