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
        : this(attributes, schemaUrl: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Resource"/> class.
    /// </summary>
    /// <param name="attributes">An <see cref="IEnumerable{T}"/> of attributes that describe the resource.</param>
    /// <param name="schemaUrl">The Schema URL (semantic conventions URL) that applies to the resource, or <see langword="null"/> if the resource has no Schema URL.</param>
#pragma warning disable CA1054 // Change the type of parameter from 'string' to 'System.Uri'
    public Resource(IEnumerable<KeyValuePair<string, object>> attributes, string? schemaUrl)
#pragma warning restore CA1054 // Change the type of parameter from 'string' to 'System.Uri'
        : this(attributes, schemaUrl, schemaUrlConflict: false)
    {
    }

    private Resource(IEnumerable<KeyValuePair<string, object>> attributes, string? schemaUrl, bool schemaUrlConflict)
    {
        this.SchemaUrl = string.IsNullOrEmpty(schemaUrl) ? null : schemaUrl;
        this.HasSchemaUrlConflict = schemaUrlConflict;

        if (attributes == null)
        {
            OpenTelemetrySdkEventSource.Log.InvalidArgument("Create resource", "attributes", "are null");
            this.Attributes = [];
            return;
        }

        // resource creation is expected to be done a few times during app startup i.e. not on the hot path, we can copy attributes.
        this.Attributes = [.. attributes.Select(SanitizeAttribute)];
    }

    /// <summary>
    /// Gets an empty Resource.
    /// </summary>
    public static Resource Empty { get; } = new([]);

    /// <summary>
    /// Gets the collection of key-value pairs describing the resource.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object>> Attributes { get; }

#pragma warning disable CA1056 // Change the type of property from 'string' to 'System.Uri'
    /// <summary>
    /// Gets the Schema URL (semantic conventions URL) that applies to the resource, or <see langword="null"/> if the resource has no Schema URL.
    /// </summary>
    public string? SchemaUrl { get; }
#pragma warning restore CA1056 // Change the type of property from 'string' to 'System.Uri'

    /// <summary>
    /// Gets a value indicating whether a Schema URL conflict has occurred during a chain of merges.
    /// </summary>
    /// <remarks>
    /// Once a conflict occurs the merged resource keeps an empty Schema URL, and the conflict is
    /// "sticky" so that subsequent merges cannot recover a Schema URL. This keeps Build() deterministic
    /// regardless of the number and order of detectors that contribute conflicting Schema URLs.
    /// </remarks>
    internal bool HasSchemaUrlConflict { get; }

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

        // A Schema URL conflict is sticky: if either resource in the chain already had a conflict the
        // merged resource keeps an empty Schema URL, regardless of the other resource's Schema URL.
        var conflict = this.HasSchemaUrlConflict || (other?.HasSchemaUrlConflict ?? false);

        var mergedSchemaUrl = MergeSchemaUrl(this.SchemaUrl, other?.SchemaUrl, ref conflict);

        return new Resource(newAttributes, mergedSchemaUrl, conflict);
    }

    // Implements the Schema URL merge logic from
    // https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/resource/sdk.md#merge
    // where "this" is the old resource and "other" is the updating resource.
    private static string? MergeSchemaUrl(string? oldSchemaUrl, string? updatingSchemaUrl, ref bool conflict)
    {
        // If a previous merge in the chain already conflicted then the conflict is sticky: the merged
        // resource keeps an empty Schema URL and no further Schema URL can be recovered.
        if (conflict)
        {
            return null;
        }

        // If the old resource's Schema URL is empty then the result is the updating resource's Schema URL.
        if (oldSchemaUrl is not { Length: > 0 })
        {
            return updatingSchemaUrl;
        }

        // Else if the updating resource's Schema URL is empty then the result is the old resource's Schema URL.
        if (updatingSchemaUrl is not { Length: > 0 })
        {
            return oldSchemaUrl;
        }

        // Else if the Schema URLs are the same then that is the result.
        if (string.Equals(oldSchemaUrl, updatingSchemaUrl, StringComparison.Ordinal))
        {
            return oldSchemaUrl;
        }

        // Else this is a merging error: the Schema URLs are both non-empty and different. The spec leaves the
        // result undefined; we log a warning and return null consistent with the Go, Java, JavaScript, and Rust SDKs.
        OpenTelemetrySdkEventSource.Log.ResourceSchemaUrlMergeConflict(oldSchemaUrl, updatingSchemaUrl);

        conflict = true;

        return null;
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
