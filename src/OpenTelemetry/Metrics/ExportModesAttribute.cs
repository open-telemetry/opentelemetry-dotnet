// <copyright file="ExportModesAttribute.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Metrics;

/// <summary>
/// An attribute for declaring the supported <see cref="ExportModes"/> of a metric exporter.
/// </summary>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
public sealed class ExportModesAttribute : Attribute
{
    private readonly ExportModes supportedExportModes;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExportModesAttribute"/> class.
    /// </summary>
    /// <param name="supported"><see cref="ExportModes"/>.</param>
    public ExportModesAttribute(ExportModes supported)
    {
        this.supportedExportModes = supported;
    }

    /// <summary>
    /// Gets the supported <see cref="ExportModes"/>.
    /// </summary>
    public ExportModes Supported => this.supportedExportModes;
}
