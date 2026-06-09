// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.CodeAnalysis;
using OpenTelemetry.Internal;

namespace OpenTelemetry.Configuration.Declarative;

/// <summary>
/// The exception thrown when a declarative configuration document cannot be
/// loaded or contains a semantic error (unsupported <c>file_format</c>, invalid
/// substitution reference, malformed structure, etc.).
/// </summary>
[Experimental(DiagnosticDefinitions.DeclarativeConfigurationExperimentalApi, UrlFormat = DiagnosticDefinitions.ExperimentalApiUrlFormat)]
public sealed class DeclarativeConfigurationException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationException"/> class.
    /// </summary>
    public DeclarativeConfigurationException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the error.</param>
    public DeclarativeConfigurationException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeclarativeConfigurationException"/> class.
    /// </summary>
    /// <param name="message">A message describing the error.</param>
    /// <param name="innerException">The exception that is the cause of this exception.</param>
    public DeclarativeConfigurationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
