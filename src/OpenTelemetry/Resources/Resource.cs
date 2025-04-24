// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Resources;

/// <summary>
/// <see cref="Resource"/> represents a resource, which captures identifying information about the entities
/// for which telemetry is reported.
/// Use <see cref="ResourceBuilder"/> to construct resource instances.
/// </summary>
public class Resource
{
    // This implementation follows https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md

    /// <summary>
    /// Initializes a new instance of the <see cref="Resource"/> class.
    /// </summary>
    /// <param name="attributes">An <see cref="IEnumerable{T}"/> of attributes that describe the resource.</param>
    public Resource(IEnumerable<KeyValuePair<string, object>> attributes)
    {
        if (attributes == null)
        {
            OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attributes", "are null");
            this.Attributes = Enumerable.Empty<KeyValuePair<string, object>>();
            return;
        }

        // resource creation is expected to be done a few times during app startup i.e. not on the hot path, we can copy attributes.
        this.Attributes = attributes.Select(SanitizeAttribute).ToList();
    }

    /// <summary>
    /// Gets an empty Resource.
    /// </summary>
    public static Resource Empty { get; } = new Resource(Enumerable.Empty<KeyValuePair<string, object>>());

    /// <summary>
    /// Gets the collection of key-value pairs describing the resource.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> Attributes { get; }

    /// <summary>
    /// Returns a new, merged <see cref="Resource"/> by merging the old <see cref="Resource"/> with the
    /// <c>other</c> <see cref="Resource"/>. In case of a collision the other <see cref="Resource"/> takes precedence.
    /// </summary>
    /// <param name="other">The <see cref="Resource"/> that will be merged with <c>this</c>.</param>
    /// <returns><see cref="Resource"/>.</returns>
    public Resource Merge(Resource other)
    {
        var newAttributes = new Dictionary<string, object>();

        if (other != null)
        {
            foreach (var attribute in other.Attributes)
            {
                if (!newAttributes.TryGetValue(attribute.Key, out _))
                {
                    newAttributes[attribute.Key] = attribute.Value;
                }
            }
        }

        foreach (var attribute in this.Attributes)
        {
            if (!newAttributes.TryGetValue(attribute.Key, out _))
            {
                newAttributes[attribute.Key] = attribute.Value;
            }
        }

        return new Resource(newAttributes);
    }

    private static KeyValuePair<string, object> SanitizeAttribute(KeyValuePair<string, object> attribute)
    {
        string sanitizedKey;
        if (attribute.Key == null)
        {
            OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attribute key", "Attribute key should be non-null string.");
            sanitizedKey = string.Empty;
        }
        else
        {
            sanitizedKey = attribute.Key;
        }

        var sanitizedValue = SanitizeValue(attribute.Value, sanitizedKey);
        return new KeyValuePair<string, object>(sanitizedKey, sanitizedValue);
    }

    private static object SanitizeValue(object value, string keyName)
    {
        Guard.ThrowIfNull(keyName);

        return value switch
        {
            string => value,
            bool => value,
            double => value,
            long => value,
            string[] => value,
            bool[] => value,
            double[] => value,
            long[] => value,
            int => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            short => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            float => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            int[] v => Array.ConvertAll(v, Convert.ToInt64),
            short[] v => Array.ConvertAll(v, Convert.ToInt64),
            float[] v => Array.ConvertAll(v, f => Convert.ToDouble(f, CultureInfo.InvariantCulture)),
            _ => throw new ArgumentException("Attribute value type is not an accepted primitive", keyName),
        };
    }
}
