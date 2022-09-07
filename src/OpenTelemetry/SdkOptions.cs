// <copyright file="SdkOptions.cs" company="OpenTelemetry Authors">
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
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace OpenTelemetry
{
    public sealed class SdkOptions
    {
        private static Action<SdkOptions>? globalConfigureCallbacks;

        public bool TracingEnabled { get; set; } = true;

        public bool MetricsEnabled { get; set; } = true;

        public bool LoggingEnabled { get; set; } = true;

        public static void RegisterConfigureCallback(Action<SdkOptions> configure)
        {
            globalConfigureCallbacks += configure ?? throw new ArgumentNullException(nameof(configure));
        }

        internal static void InvokeConfigureCallbacks(SdkOptions options)
        {
            Debug.Assert(options != null, "options was null");

            globalConfigureCallbacks?.Invoke(options);
        }

        internal sealed class ConfigureSdkOptions : IConfigureOptions<SdkOptions>
        {
            private readonly IConfiguration configuration;

            public ConfigureSdkOptions(IConfiguration configuration)
            {
                Debug.Assert(configuration != null, "configuration was null");

                this.configuration = configuration;
            }

            public void Configure(SdkOptions options)
            {
                Debug.Assert(options != null, "options was null");

                var globalDisableFlagValue = this.configuration.GetValue("OTEL_SDK_DISABLED", false);
                if (globalDisableFlagValue)
                {
                    options.TracingEnabled = false;
                    options.MetricsEnabled = false;
                    options.LoggingEnabled = false;
                }

                InvokeConfigureCallbacks(options);
            }
        }
    }
}
