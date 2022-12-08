// <copyright file="ProviderBuilderServiceCollectionCallbackHelper.cs" company="OpenTelemetry Authors">
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

using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace OpenTelemetry;

internal static class ProviderBuilderServiceCollectionCallbackHelper<TBuilder, TProvider, TState>
    where TState : ProviderBuilderState<TBuilder, TProvider>
{
    public static IServiceCollection RegisterConfigureBuilderCallback(
        IServiceCollection services,
        Action<IServiceProvider, TBuilder> configure)
    {
        Debug.Assert(configure != null, "configure was null");

        return RegisterConfigureStateCallback(
            services,
            (sp, state) => configure!(sp, state.Builder));
    }

    public static IServiceCollection RegisterConfigureStateCallback(
        IServiceCollection services,
        Action<IServiceProvider, TState> configure)
    {
        Debug.Assert(services != null, "services was null");
        Debug.Assert(configure != null, "configure was null");

        return services!.AddSingleton(
            new ConfigureProviderBuilderStateCallbackRegistration(configure!));
    }

    public static void InvokeRegisteredConfigureStateCallbacks(
        IServiceProvider serviceProvider,
        TState state)
    {
        Debug.Assert(serviceProvider != null, "serviceProvider was null");
        Debug.Assert(state != null, "state was null");

        var callbackRegistrations = serviceProvider!.GetServices<ConfigureProviderBuilderStateCallbackRegistration>();

        foreach (var callbackRegistration in callbackRegistrations)
        {
            callbackRegistration.Configure(serviceProvider!, state!);
        }
    }

    private sealed class ConfigureProviderBuilderStateCallbackRegistration
    {
        private readonly Action<IServiceProvider, TState> configure;

        public ConfigureProviderBuilderStateCallbackRegistration(
            Action<IServiceProvider, TState> configure)
        {
            this.configure = configure;
        }

        public void Configure(IServiceProvider serviceProvider, TState state)
        {
            Debug.Assert(serviceProvider != null, "serviceProvider was null");
            Debug.Assert(state != null, "state was null");

            this.configure(serviceProvider!, state!);
        }
    }
}
