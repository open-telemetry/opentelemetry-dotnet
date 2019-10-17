// <copyright file="LightStepReport.cs" company="OpenTelemetry Authors">
// Copyright 2018, OpenTelemetry Authors
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
using System.Collections.Generic;
using Newtonsoft.Json;

#pragma warning disable SA1402 // File may only contain a single type

namespace OpenTelemetry.Exporter.LightStep.Implementation
{
    internal class LightStepReport
    {
        [JsonProperty("auth")]
        public Authentication Auth { get; set; }

        [JsonProperty("reporter")]
        public Reporter Reporter { get; set; }

        [JsonProperty("spans")]
        public IList<LightStepSpan> Spans { get; set; } = new List<LightStepSpan>();
    }

    internal class Authentication
    {
        [JsonProperty("access_token")]
        public string AccessToken { get; set; }
    }

    internal class Reporter
    {
        [JsonProperty("reporter_id")]
        public int ReporterId { get; set; }

        [JsonProperty("tags")]
        public IList<Tag> Tags { get; set; } = new List<Tag>();
    }
}
