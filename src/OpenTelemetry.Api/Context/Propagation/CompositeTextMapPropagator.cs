// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using OpenTelemetry.Internal;

namespace OpenTelemetry.Context.Propagation;

/// <summary>
/// CompositeTextMapPropagator provides a mechanism for combining multiple
/// textmap propagators into a single one.
/// </summary>
public class CompositeTextMapPropagator : TextMapPropagator
{
    private static readonly ISet<string> EmptyFields = new HashSet<string>();
    private readonly List<TextMapPropagator> propagators;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTextMapPropagator"/> class.
    /// </summary>
    /// <param name="propagators">List of <see cref="TextMapPropagator"/> wire context propagator.</param>
    public CompositeTextMapPropagator(IEnumerable<TextMapPropagator> propagators)
    {
        Guard.ThrowIfNull(propagators);

        this.propagators = new List<TextMapPropagator>(propagators);
    }

    /// <inheritdoc/>
    public override ISet<string> Fields => EmptyFields;

    /// <inheritdoc/>
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>> getter)
    {
        foreach (var propagator in this.propagators)
        {
            context = propagator.Extract(context, carrier, getter);
        }

        return context;
    }

    /// <inheritdoc/>
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        foreach (var propagator in this.propagators)
        {
            propagator.Inject(context, carrier, setter);
        }
    }
}