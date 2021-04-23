// <copyright file="GrpcServer.netfx.cs" company="OpenTelemetry Authors">
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
#if NETFRAMEWORK
using System;
using Greet;
using Grpc.Core;
using Microsoft.Extensions.Logging.Abstractions;
using OpenTelemetry.Instrumentation.Grpc.Tests.Services;

namespace OpenTelemetry.Instrumentation.Grpc.Tests
{
    public class GrpcServer : IDisposable
    {
        private static readonly Random GlobalRandom = new Random();

        private readonly Server server;

        public GrpcServer()
        {
            this.Port = 0;

            var retryCount = 5;
            while (retryCount > 0)
            {
                try
                {
                    this.Port = GlobalRandom.Next(2000, 5000);
                    this.server = new Server
                    {
                        Services = { Greeter.BindService(new GreeterService(new NullLoggerFactory())) },
                        Ports = { new ServerPort("localhost", this.Port, ServerCredentials.Insecure) },
                    };
                    this.server.Start();
                    break;
                }
                catch (System.IO.IOException)
                {
                    retryCount--;
                    this.server.KillAsync().Wait();
                }
            }
        }

        public int Port { get; }

        public void Dispose()
        {
            this.server?.KillAsync().Wait();
        }
    }
}
#endif
