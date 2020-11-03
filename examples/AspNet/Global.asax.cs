// <copyright file="Global.asax.cs" company="OpenTelemetry Authors">
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
using System.Configuration;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Routing;
using OpenTelemetry;
using OpenTelemetry.Context.Propagation;
using OpenTelemetry.Trace;

namespace Examples.AspNet
{
#pragma warning disable SA1649 // File name should match first type name
    public class WebApiApplication : HttpApplication
#pragma warning restore SA1649 // File name should match first type name
    {
        private IDisposable tracerProvider;

        protected void Application_Start()
        {
            var builder = Sdk.CreateTracerProviderBuilder()
                 .AddAspNetInstrumentation(options => options.Propagator = new B3Propagator())
                 .AddHttpClientInstrumentation(
                        httpClientOptions => httpClientOptions.Propagator = new B3Propagator(),
                        httpWebRequestOptions => httpWebRequestOptions.Propagator = new B3Propagator());

            switch (ConfigurationManager.AppSettings["UseExporter"].ToLowerInvariant())
            {
                case "jaeger":
                    builder.AddJaegerExporter(jaegerOptions =>
                     {
                         jaegerOptions.AgentHost = ConfigurationManager.AppSettings["JaegerHost"];
                         jaegerOptions.AgentPort = int.Parse(ConfigurationManager.AppSettings["JaegerPort"]);
                     });
                    break;
                case "zipkin":
                    builder.AddZipkinExporter(zipkinOptions =>
                    {
                        zipkinOptions.Endpoint = new Uri(ConfigurationManager.AppSettings["ZipkinEndpoint"]);
                    });
                    break;
                default:
                    Console.SetOut(new DebugStreamWriter());
                    builder.AddConsoleExporter();
                    break;
            }

            this.tracerProvider = builder.Build();

            GlobalConfiguration.Configure(WebApiConfig.Register);

            AreaRegistration.RegisterAllAreas();
            RouteConfig.RegisterRoutes(RouteTable.Routes);
        }

        protected void Application_End()
        {
            this.tracerProvider?.Dispose();
        }

        private class DebugStreamWriter : StreamWriter
        {
            public DebugStreamWriter()
                : base(new DebugStream(), Encoding.Unicode, 1024)
            {
                this.AutoFlush = true;
            }

            private sealed class DebugStream : Stream
            {
                public override bool CanRead => false;

                public override bool CanSeek => false;

                public override bool CanWrite => true;

                public override long Length => throw new NotSupportedException();

                public override long Position
                {
                    get => throw new NotSupportedException();
                    set => throw new NotSupportedException();
                }

                public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

                public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

                public override void SetLength(long value) => throw new NotSupportedException();

                public override void Write(byte[] buffer, int offset, int count)
                {
                    Debug.Write(Encoding.Unicode.GetString(buffer, offset, count));
                }

                public override void Flush() => Debug.Flush();
            }
        }
    }
}
