// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics.Tracing;
using System.Reflection;

#pragma warning disable IDE0005 // Using directive is unnecessary.
using Xunit;
#pragma warning restore IDE0005 // Using directive is unnecessary.

namespace OpenTelemetry.Tests;

// Adapted from https://github.com/dotnet/aspnetcore/blob/3a973a5f4d28242262f27c86eb3f14299fe712ba/src/Testing/test/EventSourceValidatorTests.cs

internal static class EventSourceTestHelper
{
    /// <summary>
    /// Validates all <c>[Event]</c>-attributed methods on <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">A type that derives from <see cref="EventSource"/>.</typeparam>
    public static void ValidateEventSourceIds<T>()
        where T : EventSource
        => ValidateEventSourceIds(typeof(T));

    /// <summary>
    /// Validates all <c>[Event]</c>-attributed methods on the given <see cref="EventSource"/>-derived type.
    /// <para>
    /// Uses <see cref="EventSource.GenerateManifest(Type, string, EventManifestOptions)"/> with
    /// <see cref="EventManifestOptions.Strict"/> to perform IL-level validation that the integer
    /// argument passed to each <c>WriteEvent</c> call matches the <c>[Event(id)]</c> attribute
    /// on the calling method. This is the same validation the .NET runtime itself uses.
    /// </para>
    /// <para>
    /// Additionally checks for duplicate <see cref="EventAttribute.EventId"/> values across methods.
    /// </para>
    /// </summary>
    /// <param name="eventSourceType">A type that derives from <see cref="EventSource"/>.</param>
    public static void ValidateEventSourceIds(Type eventSourceType)
    {
        Assert.NotNull(eventSourceType);

        Assert.True(
            typeof(EventSource).IsAssignableFrom(eventSourceType),
            $"Type '{eventSourceType.FullName}' does not derive from EventSource.");

        var errors = new List<string>();

        // Check for duplicate Event IDs across methods.
        var seenIds = new Dictionary<int, string>();
        var methods = eventSourceType.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        foreach (var method in methods)
        {
            var eventAttr = method.GetCustomAttribute<EventAttribute>();
            if (eventAttr is null)
            {
                continue;
            }

            if (seenIds.TryGetValue(eventAttr.EventId, out var existingMethod))
            {
                errors.Add(
                    $"Duplicate EventId {eventAttr.EventId}: methods '{existingMethod}' and '{method.Name}' share the same ID.");
            }
            else
            {
                seenIds[eventAttr.EventId] = method.Name;
            }
        }

        // Use GenerateManifest with Strict mode to validate that each method's
        // WriteEvent(id, ...) call uses an ID that matches its [Event(id)] attribute.
        // Internally this uses GetHelperCallFirstArg to IL-inspect the method body
        // and extract the integer constant passed to WriteEvent - the same validation
        // the .NET runtime performs when constructing an EventSource.
        try
        {
            var manifest = EventSource.GenerateManifest(
                eventSourceType,
                assemblyPathToIncludeInManifest: "assemblyPathForValidation",
                flags: EventManifestOptions.Strict);

            if (manifest is null)
            {
                errors.Add("GenerateManifest returned null, indicating the type is not a valid EventSource.");
            }
        }
        catch (ArgumentException ex)
        {
            errors.Add(ex.Message);
        }

        if (errors.Count > 0)
        {
            Assert.Fail(
                $"EventSource '{eventSourceType.FullName}' has event ID validation error(s):" +
                Environment.NewLine + string.Join(Environment.NewLine, errors));
        }
    }
}
