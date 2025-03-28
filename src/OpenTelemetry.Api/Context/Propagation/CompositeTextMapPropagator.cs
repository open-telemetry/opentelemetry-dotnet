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
    private readonly List<TextMapPropagator> propagators;
    private readonly ISet<string> allFields;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompositeTextMapPropagator"/> class.
    /// </summary>
    /// <param name="propagators">List of <see cref="TextMapPropagator"/> wire context propagator.</param>
    public CompositeTextMapPropagator(IEnumerable<TextMapPropagator> propagators)
    {
        Guard.ThrowIfNull(propagators);

        var propagatorsList = new List<TextMapPropagator>();

        foreach (var propagator in propagators)
        {
            if (propagator is not null)
            {
                propagatorsList.Add(propagator);
            }
        }

        this.propagators = propagatorsList;

        // For efficiency, we resolve the fields from all propagators only once, as they are
        // not expected to change (although the implementation doesn't strictly prevent that).
        if (this.propagators.Count == 0)
        {
            // Use a new empty HashSet for each instance to avoid any potential mutation issues.
            this.allFields = new HashSet<string>();
        }
        else
        {
            ISet<string>? fields = this.propagators[0].Fields;

            var output = fields is not null
                ? new HashSet<string>(fields)
                : [];

            for (int i = 1; i < this.propagators.Count; i++)
            {
                fields = this.propagators[i].Fields;
                if (fields is not null)
                {
                    output.UnionWith(fields);
                }
            }

            this.allFields = output;
        }
    }

    /// <inheritdoc/>
    public override ISet<string> Fields => this.allFields;

    /// <inheritdoc/>
    public override PropagationContext Extract<T>(PropagationContext context, T carrier, Func<T, string, IEnumerable<string>?> getter)
    {
        for (int i = 0; i < this.propagators.Count; i++)
        {
            context = this.propagators[i].Extract(context, carrier, getter);
        }

        return context;
    }

    /// <inheritdoc/>
    public override void Inject<T>(PropagationContext context, T carrier, Action<T, string, string> setter)
    {
        for (int i = 0; i < this.propagators.Count; i++)
        {
            this.propagators[i].Inject(context, carrier, setter);
        }
    }
}
