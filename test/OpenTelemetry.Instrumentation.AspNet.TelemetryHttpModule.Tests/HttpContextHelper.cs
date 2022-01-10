// <copyright file="HttpContextHelper.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Instrumentation.AspNet.Tests
{
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Threading;
    using System.Web;
    using System.Web.Hosting;

    internal class HttpContextHelper
    {
        public static HttpContext GetFakeHttpContext(string page = "/page", string query = "", IDictionary<string, string> headers = null)
        {
            Thread.GetDomain().SetData(".appPath", string.Empty);
            Thread.GetDomain().SetData(".appVPath", string.Empty);

            var workerRequest = new SimpleWorkerRequestWithHeaders(page, query, new StringWriter(CultureInfo.InvariantCulture), headers);
            var context = new HttpContext(workerRequest);
            HttpContext.Current = context;
            return context;
        }

        public static HttpContextBase GetFakeHttpContextBase(string page = "/page", string query = "", IDictionary<string, string> headers = null)
        {
            var context = GetFakeHttpContext(page, query, headers);
            return new HttpContextWrapper(context);
        }

        private class SimpleWorkerRequestWithHeaders : SimpleWorkerRequest
        {
            private readonly IDictionary<string, string> headers;

            public SimpleWorkerRequestWithHeaders(string page, string query, TextWriter output, IDictionary<string, string> headers)
                : base(page, query, output)
            {
                if (headers != null)
                {
                    this.headers = headers;
                }
                else
                {
                    this.headers = new Dictionary<string, string>();
                }
            }

            public override string[][] GetUnknownRequestHeaders()
            {
                List<string[]> result = new List<string[]>();

                foreach (var header in this.headers)
                {
                    result.Add(new string[] { header.Key, header.Value });
                }

                var baseResult = base.GetUnknownRequestHeaders();
                if (baseResult != null)
                {
                    result.AddRange(baseResult);
                }

                return result.ToArray();
            }

            public override string GetUnknownRequestHeader(string name)
            {
                if (this.headers.ContainsKey(name))
                {
                    return this.headers[name];
                }

                return base.GetUnknownRequestHeader(name);
            }

            public override string GetKnownRequestHeader(int index)
            {
                var name = GetKnownRequestHeaderName(index);

                if (this.headers.ContainsKey(name))
                {
                    return this.headers[name];
                }

                return base.GetKnownRequestHeader(index);
            }
        }
    }
}
