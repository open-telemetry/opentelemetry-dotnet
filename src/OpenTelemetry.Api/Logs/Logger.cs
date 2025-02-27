// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

#if NET && EXPOSE_EXPERIMENTAL_FEATURES
using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;
#endif

namespace OpenTelemetry.Logs;

#if EXPOSE_EXPERIMENTAL_FEATURES
/// <summary>
/// Logger is the class responsible for creating log records.
/// </summary>
/// <remarks><b>WARNING</b>: This is an experimental API which might change or be removed in the future. Use at your own risk.</remarks>
#if NET
[Experimental(DiagnosticDefinitions.LogsBridgeExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
#endif
public
#else
/// <summary>
/// Logger is the class responsible for creating log records.
/// </summary>
internal
#endif
abstract class Logger
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Logger"/> class.
    /// </summary>
    /// <param name="name">Optional name identifying the instrumentation library.</param>
    protected Logger(string? name)
    {
        this.Name = string.IsNullOrWhiteSpace(name)
            ? string.Empty
            : name!;
    }

    /// <summary>
    /// Gets the name identifying the instrumentation library.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the version of the instrumentation library.
    /// </summary>
    public string? Version { get; private set; }

    /// <summary>
    /// Gets the attributes of the instrumentation library.
    /// </summary>
    public IEnumerable<KeyValuePair<string, object?>>? Attributes { get; private set; }

    /// <summary>
    /// Emit a log.
    /// </summary>
    /// <param name="data"><see cref="LogRecordData"/>.</param>
    public void EmitLog(in LogRecordData data)
        => this.EmitLog(in data, default);

    /// <summary>
    /// Emit a log.
    /// </summary>
    /// <param name="data"><see cref="LogRecordData"/>.</param>
    /// <param name="attributes"><see cref="LogRecordAttributeList"/>.</param>
    public abstract void EmitLog(
        in LogRecordData data,
        in LogRecordAttributeList attributes);

    internal void SetInstrumentationScope(
        string? version,
        IEnumerable<KeyValuePair<string, object?>>? attributes)
    {
        this.Version = version;

        if (attributes is not null)
        {
            var attributeList = new List<KeyValuePair<string, object?>>(attributes);
            attributeList.Sort((left, right) => string.Compare(left.Key, right.Key, StringComparison.Ordinal));
            this.Attributes = attributeList.AsReadOnly();

            this.Attributes = attributeList;
        }
    }
}
