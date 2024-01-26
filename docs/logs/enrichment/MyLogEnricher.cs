// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Microsoft.Extensions.Logging;

public class MyLogEnricher : ILogEnricher
{
    public void Enrich(IEnrichmentTagCollector collector)
    {
        collector.Add("CustomTag", "CustomValue");
    }
}
