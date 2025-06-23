// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Resources;

internal sealed class MyResourceDetector : IResourceDetector
{
    public Resource Detect()
    {
        var attributes = new List<KeyValuePair<string, object>>
        {
            new KeyValuePair<string, object>("key", "val"),
        };

        return new Resource(attributes);
    }
}
