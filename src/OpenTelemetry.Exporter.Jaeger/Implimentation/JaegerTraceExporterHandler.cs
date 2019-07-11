﻿// <copyright file="JaegerTraceExporterHandler.cs" company="OpenTelemetry Authors">// Copyright 2018, OpenTelemetry Authors//// Licensed under the Apache License, Version 2.0 (the "License");// you may not use this file except in compliance with the License.// You may obtain a copy of the License at////     http://www.apache.org/licenses/LICENSE-2.0//// Unless required by applicable law or agreed to in writing, software// distributed under the License is distributed on an "AS IS" BASIS,// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.// See the License for the specific language governing permissions and// limitations under the License.// </copyright>namespace OpenTelemetry.Exporter.Jaeger.Implimentation{    using System;    using System.Collections.Generic;    using System.Linq;    using System.Net.Sockets;    using System.Threading;    using System.Threading.Tasks;    using OpenTelemetry.Trace;    using OpenTelemetry.Trace.Export;    public class JaegerTraceExporterHandler : IHandler, IDisposable    {        public const string DefaultAgentUdpHost = "localhost";        public const int DefaultAgentUdpCompactPort = 6831;        public const int DefaultMaxPacketSize = 65000;        private readonly JaegerExporterOptions options;        private readonly JaegerUdpBatcher jaegerAgentUdpBatcher;        private bool disposedValue = false; // To detect redundant dispose calls        public JaegerTraceExporterHandler(JaegerExporterOptions options)        {            this.ValidateOptions(options);            this.InitializeOptions(options);            this.options = options;            this.jaegerAgentUdpBatcher = new JaegerUdpBatcher(options);        }        public async Task ExportAsync(IEnumerable<SpanData> spanDataList)        {            var jaegerspans = spanDataList.Select(sdl => sdl.ToJaegerSpan());            foreach (var s in jaegerspans)            {                await this.jaegerAgentUdpBatcher.AppendAsync(s, CancellationToken.None);            }            await this.jaegerAgentUdpBatcher.FlushAsync(CancellationToken.None);        }        public void Dispose()        {            // Do not change this code. Put cleanup code in Dispose(bool disposing).            this.Dispose(true);        }        protected virtual void Dispose(bool disposing)        {            if (!this.disposedValue)            {                if (disposing)                {                    this.jaegerAgentUdpBatcher.Dispose();                }                this.disposedValue = true;            }        }        private void ValidateOptions(JaegerExporterOptions options)        {            if (string.IsNullOrWhiteSpace(options.ServiceName))            {                throw new ArgumentNullException("ServiceName", "Service Name is required.");            }        }        private void InitializeOptions(JaegerExporterOptions options)        {            if (string.IsNullOrWhiteSpace(options.AgentHost))            {                options.AgentHost = DefaultAgentUdpHost;            }            if (!options.AgentPort.HasValue)            {                options.AgentPort = DefaultAgentUdpCompactPort;            }            if (!options.MaxPacketSize.HasValue)            {                options.MaxPacketSize = DefaultMaxPacketSize;            }        }    }}