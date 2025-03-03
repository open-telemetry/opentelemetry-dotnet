// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using Xunit;

namespace OpenTelemetry.Shims.OpenTracing.Tests;

[CollectionDefinition(nameof(ListenAndSampleAllActivitySources))]
public sealed class ListenAndSampleAllActivitySources : ICollectionFixture<ListenAndSampleAllActivitySourcesFixture>;
