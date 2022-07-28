// <copyright file="SdkConfiguration.cs" company="OpenTelemetry Authors">
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

namespace OpenTelemetry.Configuration;

internal class SdkConfiguration
{
    private int? spanAttributeValueLengthLimit;
    private int? spanAttributeCountLimit;
    private int? eventAttributeCountLimit;
    private int? linkAttributeCountLimit;

    private SdkConfiguration()
    {
        EnvironmentVariableConfiguration.InitializeDefaultConfigurationFromEnvironment(this);
    }

    public static SdkConfiguration Instance { get; private set; } = new SdkConfiguration();

    public int? AttributeValueLengthLimit { get; set; }

    public int? AttributeCountLimit { get; set; }

    public int? SpanAttributeValueLengthLimit
    {
        get => this.spanAttributeValueLengthLimit ?? this.AttributeValueLengthLimit;
        set => this.spanAttributeValueLengthLimit = value;
    }

    public int? SpanAttributeCountLimit
    {
        get => this.spanAttributeCountLimit ?? this.AttributeCountLimit;
        set => this.spanAttributeCountLimit = value;
    }

    public int? SpanEventCountLimit { get; set; }

    public int? SpanLinkCountLimit { get; set; }

    public int? EventAttributeCountLimit
    {
        get => this.eventAttributeCountLimit ?? this.SpanAttributeCountLimit;
        set => this.eventAttributeCountLimit = value;
    }

    public int? LinkAttributeCountLimit
    {
        get => this.linkAttributeCountLimit ?? this.SpanAttributeCountLimit;
        set => this.linkAttributeCountLimit = value;
    }

    internal static void Reset()
    {
        Instance = new SdkConfiguration();
    }
}
