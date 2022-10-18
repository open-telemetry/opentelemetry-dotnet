// <copyright file="MeterProviderBuilderServiceCollectionHelper.cs" company="OpenTelemetry Authors">
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

using System;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry.Metrics;

internal static class MeterProviderBuilderServiceCollectionHelper
{
    internal static IServiceCollection RegisterConfigureBuilderCallback(
        IServiceCollection services,
        Action<IServiceProvider, MeterProviderBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        return RegisterConfigureStateCallback(
            services,
            (sp, state) => configure!(sp, state.Builder));
    }

    internal static IServiceCollection RegisterConfigureStateCallback(
        IServiceCollection services,
        Action<IServiceProvider, MeterProviderBuilderState> configure)
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(configure != null, "configure was null");

        return services!.AddSingleton(new ConfigureMeterProviderBuilderStateCallbackRegistration(configure!));
    }

    internal static void InvokeRegisteredConfigureStateCallbacks(
        IServiceProvider serviceProvider,
        MeterProviderBuilderState state)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");
        Debug.Assert(state != null, "state was null");

        var callbackRegistrations = serviceProvider!.GetServices<ConfigureMeterProviderBuilderStateCallbackRegistration>();

        foreach (var callbackRegistration in callbackRegistrations)
        {
            callbackRegistration.Configure(serviceProvider!, state!);
        }
    }

    private sealed class ConfigureMeterProviderBuilderStateCallbackRegistration
    {
        private readonly Action<IServiceProvider, MeterProviderBuilderState> configure;

        public ConfigureMeterProviderBuilderStateCallbackRegistration(
            Action<IServiceProvider, MeterProviderBuilderState> configure)
        {
            this.configure = configure;
        }

        public void Configure(IServiceProvider serviceProvider, MeterProviderBuilderState state)
        {
            Debug.Assert(serviceProvider != null, "serviceProvider was null");
            Debug.Assert(state != null, "state was null");

            this.configure(serviceProvider!, state!);
        }
    }
}
