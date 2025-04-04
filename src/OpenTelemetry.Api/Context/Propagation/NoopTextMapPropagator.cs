// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

namespace OpenTelemetry.Context.Propagation;

internal sealed class NoopTextMapPropagator : TextMapPropagator
{
#pragma warning disable CA1805 // Do not initialize unnecessarily
    private static readonly PropagationContext DefaultPropagationContext = default;
#pragma warning restore CA1805 // Do not initialize unnecessarily

    public override ISet<string>? Fields => null;

    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        return DefaultPropagationContext;
    }

    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
    }
}
