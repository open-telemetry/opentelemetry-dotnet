// <copyright file="ZPagesExporterStatsHttpServerTest.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace OpenTelemetry.Exporter.ZPages.Tests
{
    public class ZPagesExporterStatsHttpServerTest
    {
        private static HttpClient httpClient = new HttpClient();

        static ZPagesExporterStatsHttpServerTest()
        {
            Activity.DefaultIdFormat = ActivityIdFormat.W3C;
            Activity.ForceDefaultIdFormat = true;

            var listener = new ActivityListener
            {
                ShouldListenTo = _ => true,
                GetRequestedDataUsingParentId = (ref ActivityCreationOptions<string> options) => ActivityDataRequest.AllData,
                GetRequestedDataUsingContext = (ref ActivityCreationOptions<ActivityContext> options) => ActivityDataRequest.AllData,
            };

            ActivitySource.AddActivityListener(listener);
        }

        [Fact]
        public async Task CheckStartStopDispose()
        {
            ActivitySource activitySource = new ActivitySource("zpages-test");

            var zpagesOptions = new ZPagesExporterOptions() { Url = "http://localhost:7284/rpcz/", RetentionTime = 3600000 };
            var zpagesExporter = new ZPagesExporter(zpagesOptions);
            using var zpagesServer = new ZPagesExporterStatsHttpServer(zpagesExporter);
            var zpagesProcessor = new ZPagesProcessor(zpagesExporter);

            zpagesServer.Start();
            await this.ValidateResult(activitySource, zpagesProcessor, "new- 01");
            await this.ValidateResult(activitySource, zpagesProcessor, "new- 02");
        }

        private async Task ValidateResult(ActivitySource activitySource, ZPagesProcessor zpagesProcessor, string name)
        {
            using (var activity = activitySource.StartActivity(name))
            {
                zpagesProcessor.OnStart(activity);
                zpagesProcessor.OnEnd(activity);
            }

            using var httpResponseMessage = await httpClient.GetAsync("http://localhost:7284/rpcz/");
            Assert.True(httpResponseMessage.IsSuccessStatusCode);

            var content = await httpResponseMessage.Content.ReadAsStringAsync();
            Assert.Contains($"<td>{name}</td>", content);
        }
    }
}
