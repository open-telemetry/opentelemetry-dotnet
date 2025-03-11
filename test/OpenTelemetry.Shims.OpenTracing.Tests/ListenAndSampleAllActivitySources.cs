// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

[CollectionDefinition(nameof(ListenAndSampleAllActivitySources))]
#pragma warning disable CA1515
public sealed class ListenAndSampleAllActivitySources : ICollectionFixture<ListenAndSampleAllActivitySourcesFixture>;
#pragma warning restore CA1515
