// <copyright file="DiagnosticDefinitions.cs" company="OpenTelemetry Authors">
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

#nullable enable

namespace OpenTelemetry.Internal;

internal static class DiagnosticDefinitions
{
    public const string ExperimentalApiUrlFormat = "https://github.com/open-telemetry/opentelemetry-dotnet/tree/main/docs/diagnostics/EXPERIMENTAL_APIS.md#{0}";

    public const string LoggerProviderExperimentalApi = "OTEL1000";
    public const string LogBridgeApiExperimentalApi = "OTEL1001";
}
