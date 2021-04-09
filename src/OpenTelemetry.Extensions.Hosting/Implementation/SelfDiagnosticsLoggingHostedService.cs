// <copyright file="SelfDiagnosticsLoggingHostedService.cs" company="OpenTelemetry Authors">
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
using System.Diagnostics.Tracing;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Extensions.Hosting.Implementation
{
    internal class SelfDiagnosticsLoggingHostedService : IHostedService
    {
        private readonly ILoggerFactory loggerFactory;
        private readonly IOptionsMonitor<LoggerFilterOptions> loggerFilterOptions;
        private IDisposable forwarder;

        public SelfDiagnosticsLoggingHostedService(ILoggerFactory loggerFactory, IOptionsMonitor<LoggerFilterOptions> loggerFilterOptions)
        {
            this.loggerFactory = loggerFactory;
            this.loggerFilterOptions = loggerFilterOptions;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            // The sole purpose of this HostedService is to
            // start forwarding the self-diagnostics events
            // to the logger factory
            this.forwarder = new SelfDiagnosticsEventLogForwarder(this.loggerFactory, this.loggerFilterOptions, EventLevel.Warning);

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            this.forwarder?.Dispose();

            return Task.CompletedTask;
        }
    }
}
