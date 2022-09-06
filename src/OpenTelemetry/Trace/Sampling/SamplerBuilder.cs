// <copyright file="SamplerBuilder.cs" company="OpenTelemetry Authors">
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

using System.Collections.Generic;

namespace OpenTelemetry.Trace;

/// <summary>
/// Contains methods for building <see cref="Sampler"/> instances.
/// </summary>
public class SamplerBuilder
{
    private readonly List<ISamplerDetector> detectors = new();
    private Sampler defaultSampler;

    private SamplerBuilder()
    {
    }

    /// <summary>
    /// Creates a <see cref="SamplerBuilder"/> instance with registered <see cref="SamplerBuilderExtensions.RegisterEnvironmentVariableDetector"/>.
    /// </summary>
    /// <returns>Created <see cref="SamplerBuilder"/>.</returns>
    public static SamplerBuilder CreateDefault()
    {
        return CreateEmpty().RegisterEnvironmentVariableDetector();
    }

    /// <summary>
    /// Creates a <see cref="SamplerBuilder"/> instance without detectors.
    /// </summary>
    /// <returns>Created <see cref="SamplerBuilder"/>.</returns>
    public static SamplerBuilder CreateEmpty()
        => new();

    /// <summary>
    /// Build a <see cref="Sampler"/> based on added detectors.
    /// Returns first detected <see cref="Sampler"/>. Detection is made in same order of registration.
    /// If all registered detectors returns null it returns default value set by <see cref="SetDefaultSampler"/> or ParentBased(root=AlwaysOnSampler).
    /// </summary>
    /// <returns><see cref="Sampler"/>.</returns>
    public Sampler Build()
    {
        foreach (var samplerDetector in this.detectors)
        {
            var sampler = samplerDetector?.Detect();
            if (sampler != null)
            {
                return sampler;
            }
        }

        return this.defaultSampler ?? new ParentBasedSampler(new AlwaysOnSampler());
    }

    /// <summary>
    /// Register detector used to Build <see cref="Sampler"/>.
    /// </summary>
    /// <param name="samplerDetector"><see cref="ISamplerDetector"/>.</param>
    /// <returns><see cref="SamplerBuilder"/> for chaining.</returns>
    public SamplerBuilder RegisterDetector(ISamplerDetector samplerDetector)
    {
        this.detectors.Add(samplerDetector);
        return this;
    }

    /// <summary>
    /// Clears the <see cref="ISamplerDetector"/>s registered in the builder.
    /// Set default <see cref="Sampler"/> to ParentBased(root=AlwaysOnSampler).
    /// </summary>
    /// <returns><see cref="SamplerBuilder"/> for chaining.</returns>
    public SamplerBuilder Clear()
    {
        this.detectors.Clear();
        this.defaultSampler = null;

        return this;
    }

    /// <summary>
    /// Set <see param="sampler"/> as a default sampler.
    /// </summary>
    /// <param name="sampler"><see cref="Sampler"/> to be set as default.</param>
    /// <returns><see cref="SamplerBuilder"/> for chaining.</returns>
    public SamplerBuilder SetDefaultSampler(Sampler sampler)
    {
        this.defaultSampler = sampler;
        return this;
    }
}
