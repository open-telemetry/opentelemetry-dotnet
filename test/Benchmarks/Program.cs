// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using BenchmarkDotNet.Running;

var summary = BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
return summary.SelectMany((p) => p.Reports).Any((p) => !p.Success) ? 1 : 0;
