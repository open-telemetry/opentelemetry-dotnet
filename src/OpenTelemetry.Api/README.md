# OpenTelemetry .NET API

[![NuGet](https://img.shields.io/nuget/v/OpenTelemetry.Api.svg)](https://www.nuget.org/packages/OpenTelemetry.Api)
[![NuGet](https://img.shields.io/nuget/dt/OpenTelemetry.Api.svg)](https://www.nuget.org/packages/OpenTelemetry.Api)

* [Installation](#installation)
* [Introduction](#introduction)
  * [Tracing API](#tracing-api)
  * [Metrics API](#metrics-api)
* [Introduction to OpenTelemetry .NET Tracing
  API](#introduction-to-opentelemetry-net-tracing-api)
* [Instrumenting a library/application with .NET Activity
  API](#instrumenting-a-libraryapplication-with-net-activity-api)
  * [Basic usage](#basic-usage)
  * [Activity creation options](#activity-creation-options)
  * [Adding Events](#adding-events)
  * [Setting Status](#setting-status)
* [Instrumenting a library/application with OpenTelemetry.API
  Shim](#instrumenting-a-libraryapplication-with-opentelemetryapi-shim)
* [References](#references)

## Installation

```shell
dotnet add package OpenTelemetry.Api
```

## Introduction

Application developers and library authors use OpenTelemetry API to instrument
their application/library. The API only surfaces necessary abstractions to
instrument an application/library. It does not address concerns like how
telemetry is exported to a specific telemetry backend, how to sample the
telemetry, etc. The API consists of [Tracing
API](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md),
[Metrics
API](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/metrics/api.md),
[Context and Propagation
API](https://github.com/open-telemetry/opentelemetry-specification/tree/master/specification/context),
and a set of [semantic
conventions](https://github.com/open-telemetry/opentelemetry-specification/tree/master/specification/trace/semantic_conventions).

### Tracing API

[Tracing
API](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md)
allows users to generate
[Spans](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#span),
which represent a single operation within a trace. Spans can be nested to form
a trace tree. Each trace contains a root span, which typically describes the
entire operation and, optionally one or more sub-spans for its sub-operations.

### Metrics API

[Metrics
API](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/metrics/api.md)
allows users to capture measurements about the execution of a computer program
at runtime. The Metrics API is designed to process raw measurements, generally
with the intent to produce continuous summaries of those measurements.

## Introduction to OpenTelemetry .NET Tracing API

.NET runtime had `Activity` class for a long time, which was meant to be used
for tracing purposes and represents the equivalent of the OpenTelemetry
[Span](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#span).
OpenTelemetry .NET is reusing the existing `Activity` and associated classes to
represent the OpenTelemetry `Span`. This means, users can instrument their
applications/libraries to emit OpenTelemetry compatible traces by using just
the .NET Runtime.

The `Activity` and associated classes are shipped as part of
`System.Diagnostics.DiagnosticSource` nuget package. Version 5.0.0 of this
package contains improvements to `Activity` class which makes it more closely
aligned with OpenTelemetry [API
specification](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md).

Even though `Activity` enables all the scenarios OpenTelemetry supports, users
who are already familiar with OpenTelemetry terminology may find it easy to
operate with that terminology. For instance, `StartSpan` may be preferred over
`StartActivity`. To help with this transition, the OpenTelemetry.API package
has [shim](#instrumenting-a-libraryapplication-with-opentelemetryapi-shim)
classes to wrap around the .NET `Activity` classes.

The shim exist only in the API. OpenTelemetry SDK for .NET will be operating
entirely with `Activity` only. Irrespective of whether shim classes or
`Activity` is used for instrumentation, the end result would be same. i.e
Processors/Exporters see the same data.

## Instrumenting a library/application with .NET Activity API

### Basic usage

As mentioned in the introduction, the instrumentation API for OpenTelemetry
.NET is the .NET `Activity` API. Guidance for instrumenting using this API is
documented fully in the TBD(dotnet activity user guide link), but is described
here as well.

1. Install the `System.Diagnostics.DiagnosticSource` package version
   5.0.0-preview.7.20364.11 or above to your application or library.

    ```xml
    <ItemGroup>
      <PackageReference
        Include="System.Diagnostics.DiagnosticSource"
        Version="5.0.0-preview.7.20364.11"
      />
    </ItemGroup>
    ```

2. Create an `ActivitySource`, providing the name and version of the
   library/application being instrumented. `ActivitySource` instance is
   typically created once and is reused throughout the application/library.

    ```csharp
    static ActivitySource activitySource = new ActivitySource(
        "companyname.product.library",
        "semver1.0.0");
    ```

    The above requires import of the `System.Diagnostics` namespace.

3. Use the `ActivitySource` instance from above to create `Activity` instances,
   which represent a single operation within a trace. The parameter passed is
   the `DisplayName` of the activity.

    ```csharp
    var activity = activitySource.StartActivity("ActivityName");
    ```

    If there are no listeners interested in this activity, the activity above
    will be null. Ensure that all subsequent calls using this activity is
    protected with a null check.

4. Populate activity with tags following the [OpenTelemetry semantic
   conventions](https://github.com/open-telemetry/opentelemetry-specification/tree/master/specification/trace/semantic_conventions).
   It is highly recommended to check `activity.IsAllDataRequested`, before
   populating any tags which are not readily available.

    ```csharp
    activity?.AddTag("http.method", "GET");
    if (activity?.IsAllDataRequested ?? false)
    {
        activity.AddTag("http.url", "http://www.mywebsite.com");
    }
    ```

5. Perform application/library logic.

6. Stop the activity when done.

    ```csharp
    activity?.Stop();
    ```

    Alternately, as `Activity` implements `IDisposable`, it can be used with a
    `using` block, which ensures activity gets stopped upon disposal. This is
    shown below.

    ```csharp
    using (var activity = activitySource.StartActivity("ActivityName")
    {
        activity?.AddTag("http.method", "GET");
    } // Activity gets stopped automatically at end of this block during dispose.
    ```

The above showed the basic usage of instrumenting using `Activity`. The
following sections describes more features.

### Activity creation options

Basic usage example above showed how `StartActivity` method can be used to
start an `Activity`. The started activity will automatically becomes the
`Current` activity. It is important to note that the `StartActivity` returns
`null`, if no listeners are interested in the activity to be created. This
happens when the final application does not enable OpenTelemetry, or when
OpenTelemetry samplers chose not to sample this activity.

`StartActivity` has many overloads to control the activity creation.

1. `ActivityKind`

    `Activity` has a property called `ActivityKind` which represents
    OpenTelemetry
    [SpanKind](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#spankind).
    The default value will be `Internal`. `StartActivity` allows passing the
    `ActivityKind` while starting an `Activity`.

    ```csharp
    var activity = activitySource.StartActivity("ActivityName", ActivityKind.Server);
    ```

2. Parent using `ActivityContext`

    `ActivityContext` represents the OpenTelemetry
    [SpanContext](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#spancontext).
    While starting a new `Activity`, the currently active `Activity` is
    automatically taken as the parent of the new activity being created.
    `StartActivity` allows passing explicit `ActivityContext` to override this
    behavior.

    ```csharp
    var parentContext = new ActivityContext(
        ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
        ActivitySpanId.CreateFromString("b7ad6b7169203331"),
        ActivityTraceFlags.None);

    var activity = activitySource.StartActivity(
        "ActivityName",
        ActivityKind.Server,
        parentContext);
    ```

    As `ActivityContext` follows the [W3C
    Trace-Context](https://w3c.github.io/trace-context), it is also possible to
    provide the parent context as a single string matching the `traceparent`
    header of the W3C Trace-Context. This is shown below.

    ```csharp
    var activity = activitySource.StartActivity(
        "ActivityName",
        ActivityKind.Server,
        "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01");
    ```

3. Initial Tags

   `Tags` in `Activity` represents the OpenTelemetry [Span
   Attributes](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#set-attributes).
   Earlier sample showed the usage of `AddTag` method of `Activity` to add
   tags. It is also possible to provide an initial set of tags during activity
   creation, as shown below.

    ```csharp
    var initialTags = new List<KeyValuePair<string, string>>();

    initialTags.Add(new KeyValuePair<string, string>("tag1", "tagValue1"));
    initialTags.Add(new KeyValuePair<string, string>("tag2", "tagValue2"));

    var activity = activitySource.StartActivity(
        "ActivityName",
        ActivityKind.Server,
        "00-0af7651916cd43dd8448eb211c80319c-b7ad6b7169203331-01",
        initialTags);
    ```

    The above requires import of the `System.Collections.Generic` namespace.

4. Activity Links

   Apart from the parent-child relation, activities can be linked using
   `ActivityLinks` which represent the OpenTelemetry
   [Links](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/overview.md#links-between-spans).
   The linked activities must be provided during the creation time, as shown
   below.

    ```csharp
    var activityLinks = new List<ActivityLink>();

    var linkedContext1 = new ActivityContext(
        ActivityTraceId.CreateFromString("0af7651916cd43dd8448eb211c80319c"),
        ActivitySpanId.CreateFromString("b7ad6b7169203331"),
        ActivityTraceFlags.None);

    var linkedContext2 = new ActivityContext(
        ActivityTraceId.CreateFromString("4bf92f3577b34da6a3ce929d0e0e4736"),
        ActivitySpanId.CreateFromString("00f067aa0ba902b7"),
        ActivityTraceFlags.Recorded);

    activityLinks.Add(new ActivityLink(linkedContext1));
    activityLinks.Add(new ActivityLink(linkedContext2));

    var activity = activitySource.StartActivity(
        "ActivityWithLinks",
        ActivityKind.Server,
        default(ActivityContext), // this creates Activity without parent
        initialTags,
        activityLinks);
    ```

    In case activity has parent, pass parent's context:

    ```csharp
    var parentContext = Activity.Current != null ? Activity.Current.Context : default(ActivityContext);

    var activity = activitySource.StartActivity(
        "ActivityWithLinks",
        ActivityKind.Server,
        parentContext,
        initialTags,
        activityLinks);
    ```

### Adding Events

It is possible to [add
event](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#add-events)
with `Activity` using the `AddEvent` method as shown below.

```csharp
activity?.AddEvent(new ActivityEvent("sample activity event."));
```

Apart from providing name, timestamp and attributes can be provided by using
corresponding overloads of `ActivityEvent`.

### Setting Status

OpenTelemetry defines a concept called
[Status](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#set-status)
to be associated with `Activity`. There is no `Status` class in .NET, and hence
`Status` is set to an `Activity` using the following special tags

`ot.status_code` is the `Tag` name used to store the [Status Canonical
Code](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#statuscanonicalcode).
`ot.status_description` is the `Tag` name used to store the optional
[Description](https://github.com/open-telemetry/opentelemetry-specification/blob/master/specification/trace/api.md#getdescription)

Example:

```csharp
activity?.AddTag("ot.status_code", "status canonical code");
activity?.AddTag("ot.status_description", "status description");
```

## Instrumenting a library/application with OpenTelemetry.API Shim

This section to be filled after shim is shipped.

## References

* [OpenTelemetry Project](https://opentelemetry.io/)
