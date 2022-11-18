// <copyright file="ConfigureProviderBuilderCallbackWrapper.cs" company="OpenTelemetry Authors">
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
using OpenTelemetry;
using OpenTelemetry.Internal;

namespace Microsoft.Extensions.DependencyInjection;

internal sealed class ConfigureProviderBuilderCallbackWrapper<TProvider, TProviderBuilder> : IConfigureProviderBuilder<TProvider, TProviderBuilder>
{
    private readonly Action<IServiceProvider, TProviderBuilder> configure;

    public ConfigureProviderBuilderCallbackWrapper(Action<IServiceProvider, TProviderBuilder> configure)
    {
        Guard.ThrowIfNull(configure);

        this.configure = configure;
    }

    public void ConfigureBuilder(IServiceProvider serviceProvider, TProviderBuilder providerBuilder)
    {
        this.configure(serviceProvider, providerBuilder);
    }
}
