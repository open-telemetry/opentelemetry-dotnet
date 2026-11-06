// Copyright The OpenTelemetry Authors
// SPDX-License-Identifier: Apache-2.0

using System.Diagnostics;
using System.Diagnostics.Tracing;
using System.Globalization;
using System.Reflection;

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

    /// <summary>
    /// Invokes every method declared on the <see cref="EventSource"/> exposed by the static
    /// <c>Log</c> member of <typeparamref name="T"/> and verifies that each <c>[Event]</c> method
    /// writes an event with the expected ID and payload.
    /// </summary>
    /// <typeparam name="T">A type that derives from <see cref="EventSource"/> and has a static <c>Log</c> member.</typeparam>
    public static void ExerciseEventSource<T>()
        where T : EventSource
    {
        const BindingFlags StaticMembers = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

        var logField = typeof(T).GetField("Log", StaticMembers);
        var logProperty = typeof(T).GetProperty("Log", StaticMembers);

        var instance = (logField?.GetValue(null) ?? logProperty?.GetValue(null)) as T;
        Assert.True(instance is not null, $"Type '{typeof(T).FullName}' does not expose a static 'Log' member.");

        ExerciseEventSource(instance);
    }

    /// <summary>
    /// Invokes every method declared on the given <see cref="EventSource"/> with synthetic arguments
    /// and verifies that each <c>[Event]</c> method writes an event with the expected ID, that the payload
    /// contains the values that were passed in, and that the message template can be formatted.
    /// <para>
    /// Generic methods and methods with <see langword="ref struct"/> parameters (such as
    /// <see cref="ReadOnlySpan{T}"/>) cannot be invoked using reflection and are skipped.
    /// </para>
    /// </summary>
    /// <param name="eventSource">The event source to exercise.</param>
    public static void ExerciseEventSource(EventSource eventSource)
    {
        Assert.NotNull(eventSource);

        using var listener = new CapturingEventListener(eventSource);

        var methods = eventSource.GetType().GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);

        var invoked = 0;

        foreach (var method in methods)
        {
            // Skip property accessors, generic methods and overrides of EventSource members.
            if (method.IsSpecialName ||
                method.IsGenericMethodDefinition ||
                method.GetBaseDefinition().DeclaringType != method.DeclaringType)
            {
                continue;
            }

            var parameters = method.GetParameters();

            if (parameters.Any(p => IsByRefLike(p.ParameterType)))
            {
                continue;
            }

            var arguments = new object?[parameters.Length];

            for (var i = 0; i < parameters.Length; i++)
            {
                arguments[i] = CreateArgument(parameters[i].ParameterType, i);
            }

            var eventAttribute = method.GetCustomAttribute<EventAttribute>();
            listener.Events.Clear();

            try
            {
                method.Invoke(eventSource, arguments);
            }
            catch (TargetInvocationException ex)
            {
                Assert.Fail($"Invoking '{method.Name}' on '{eventSource.GetType().FullName}' threw: {ex.InnerException}");
            }

            invoked++;

            if (eventAttribute is null)
            {
                continue;
            }

            var written = listener.Events.SingleOrDefault(e => e.EventId == eventAttribute.EventId);

            Assert.True(
                written is not null,
                $"Invoking '{method.Name}' on '{eventSource.GetType().FullName}' did not write event {eventAttribute.EventId}.");

            var payload = written.Payload?.ToArray() ?? [];

            Assert.Equal(parameters.Length, payload.Length);

            for (var i = 0; i < arguments.Length; i++)
            {
                // Only simple values are compared as the event source is free to transform
                // anything else (e.g. redacting a Uri) before writing it to the payload.
                if (arguments[i] is string or bool or int or long)
                {
                    Assert.True(
                        Equals(arguments[i], payload[i]),
                        $"Event {eventAttribute.EventId} payload item {i} does not match the value passed to '{method.Name}'.");
                }
            }

            if (eventAttribute.Message is not null)
            {
                Assert.False(string.IsNullOrEmpty(written.Message), $"Event {eventAttribute.EventId} has an empty message.");

                // EventWrittenEventArgs.Message is the unformatted template, so format it
                // with the payload to verify that its placeholders are valid. The resulting
                // text is intentionally not inspected.
                try
                {
                    _ = string.Format(CultureInfo.InvariantCulture, written.Message!, payload);
                }
                catch (FormatException ex)
                {
                    Assert.Fail($"The message for event {eventAttribute.EventId} cannot be formatted with its payload: {ex.Message}");
                }
            }
        }

        Assert.True(invoked > 0, $"No methods were found on '{eventSource.GetType().FullName}'.");
    }

    private static bool IsByRefLike(Type type)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        // Type.IsByRefLike is not available on .NET Framework.
        return type.IsValueType &&
            type.CustomAttributes.Any(a => a.AttributeType.FullName == "System.Runtime.CompilerServices.IsByRefLikeAttribute");
    }

    /// <summary>
    /// Creates a synthetic argument for the parameter at the given position. Simple values
    /// are distinct per position and non-default so that a method which forwards its
    /// arguments to the payload in the wrong order, duplicates one, or writes a constant
    /// instead is detected when the payload is compared with the arguments.
    /// </summary>
    private static object? CreateArgument(Type type, int position)
    {
        if (type.IsByRef)
        {
            type = type.GetElementType()!;
        }

        type = Nullable.GetUnderlyingType(type) ?? type;

        if (type == typeof(string))
        {
            return $"argument{position}";
        }

        if (type == typeof(bool))
        {
            return true;
        }

        if (type == typeof(int))
        {
            return position + 1;
        }

        if (type == typeof(long))
        {
            return (position + 1) * 1000L;
        }

        if (type == typeof(Uri))
        {
            return new Uri("https://localhost:4318/v1/traces");
        }

        if (type == typeof(Exception))
        {
            return new InvalidOperationException("test");
        }

        if (type == typeof(Activity))
        {
            return new Activity("test");
        }

        if (type.IsArray)
        {
            return Array.CreateInstance(type.GetElementType()!, 0);
        }

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }

    private sealed class CapturingEventListener : EventListener
    {
        private readonly EventSource eventSource;
        private readonly int threadId = Environment.CurrentManagedThreadId;

        public CapturingEventListener(EventSource eventSource)
        {
            this.eventSource = eventSource;
            this.EnableEvents(eventSource, EventLevel.Verbose, EventKeywords.All);
        }

        public List<EventWrittenEventArgs> Events { get; } = [];

        public override void Dispose()
        {
            this.DisableEvents(this.eventSource);
            base.Dispose();
        }

        protected override void OnEventWritten(EventWrittenEventArgs eventData)
        {
            if (eventData.EventSource == this.eventSource && Environment.CurrentManagedThreadId == this.threadId)
            {
                this.Events.Add(eventData);
            }
        }
    }
}
