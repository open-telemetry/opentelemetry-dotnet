// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;

namespace OpenTelemetry.Exporter.Zipkin.Tests.TestUtility;

internal class ActivitySourceBuilder
{
    private readonly string _name;
    private string _version = string.Empty;
    private readonly List<KeyValuePair<string, object?>> _tags = new();

    private ActivitySourceBuilder(string name)
    {
        this._name = name;
    }

    public static ActivitySourceBuilder Create(string name) => new(name);

    public ActivitySourceBuilder WithVersion(string version)
    {
        this._version = version;
        return this;
    }

    public ActivitySourceBuilder WithTag(string key, object? value)
    {
        this._tags.Add(new KeyValuePair<string, object?>(key, value));
        return this;
    }

    public ActivitySource Build()
    {
        return new ActivitySource(this._name, this._version, this._tags.ToArray());
    }
}
