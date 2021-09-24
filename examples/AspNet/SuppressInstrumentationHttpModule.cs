// <copyright file="SuppressInstrumentationHttpModule.cs" company="OpenTelemetry Authors">
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
using System.Web;
using OpenTelemetry;

namespace Examples.AspNet
{
    /// <summary>
    /// A demo <see cref="IHttpModule"/> which will suppress ASP.NET
    /// instrumentation if a request contains "suppress=true" on the query
    /// string. Suppressed spans will not be processed/exported by the
    /// OpenTelemetry SDK.
    /// </summary>
    public class SuppressInstrumentationHttpModule : IHttpModule
    {
        private IDisposable suppressionScope;

        public void Init(HttpApplication context)
        {
            context.BeginRequest += this.Application_BeginRequest;
            context.EndRequest += this.Application_EndRequest;
        }

        public void Dispose()
        {
        }

        private void Application_BeginRequest(object sender, EventArgs e)
        {
            var context = ((HttpApplication)sender).Context;

            if (context.Request.QueryString["suppress"] == "true")
            {
                this.suppressionScope = SuppressInstrumentationScope.Begin();
            }
        }

        private void Application_EndRequest(object sender, EventArgs e)
        {
            this.suppressionScope?.Dispose();
        }
    }
}
