// <copyright file="IDeferredLoggerProviderBuilder.cs" company="OpenTelemetry Authors">
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

#if NET8_0_OR_GREATER
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Describes a logger provider builder that supports deferred
/// initialization using an <see cref="IServiceProvider"/> to perform
/// dependency injection.
/// </summary>
/// <remarks><inheritdoc cref="Logger" path="/remarks"/></remarks>
#if NET8_0_OR_GREATER
[Experimental(DiagnosticDefinitions.LoggerProviderExperimentalFeature, UrlFormat = DiagnosticDefinitions.UrlFormat)]
#endif
public
#else
/// <summary>
/// Describes a logger provider builder that supports deferred
/// initialization using an <see cref="IServiceProvider"/> to perform
/// dependency injection.
/// </summary>
internal
#endif
interface IDeferredLoggerProviderBuilder
{
    /// <summary>
    /// Register a callback action to configure the <see
    /// cref="LoggerProviderBuilder"/> once the application <see
    /// cref="IServiceProvider"/> is available.
    /// </summary>
    /// <param name="configure">Configuration callback.</param>
    /// <returns>The supplied <see cref="LoggerProviderBuilder"/> for chaining.</returns>
    LoggerProviderBuilder Configure(Action<IServiceProvider, LoggerProviderBuilder> configure);
}
