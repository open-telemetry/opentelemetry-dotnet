// <copyright file="AssemblyInfo.cs" company="OpenTelemetry Authors">
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

using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("OpenTelemetry.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("OpenTelemetry.Extensions.Hosting.Tests" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("DynamicProxyGenAssembly2" + AssemblyInfo.MoqPublicKey)]
[assembly: InternalsVisibleTo("Benchmarks" + AssemblyInfo.PublicKey)]

// TODO: Much of the metrics SDK is currently internal. These should be removed once the public API surface area for metrics is defined.
[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.OpenTelemetryProtocol" + AssemblyInfo.PublicKey)]
[assembly: InternalsVisibleTo("OpenTelemetry.Exporter.OpenTelemetryProtocol.Tests" + AssemblyInfo.PublicKey)]
