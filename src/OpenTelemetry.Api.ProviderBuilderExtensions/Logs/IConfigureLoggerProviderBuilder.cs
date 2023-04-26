// <copyright file="IConfigureLoggerProviderBuilder.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Logs;

/// <summary>
/// Represents something that configures the <see cref="LoggerProviderBuilder"/> type.
/// </summary>
// Note: This API may be made public if there is a need for it.
internal interface IConfigureLoggerProviderBuilder
{
    /// <summary>
    /// Invoked to configure a <see cref="LoggerProviderBuilder"/> instance.
    /// </summary>
    /// <param name="serviceProvider"><see cref="IServiceProvider"/>.</param>
    /// <param name="loggerProviderBuilder"><see cref="LoggerProviderBuilder"/>.</param>
    void ConfigureBuilder(IServiceProvider serviceProvider, LoggerProviderBuilder loggerProviderBuilder);
}
