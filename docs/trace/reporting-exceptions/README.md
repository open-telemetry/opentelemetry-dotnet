# Reporting Exceptions

The following doc describes how to report Exceptions to OpenTelemetry tracing
when user is manually creating Activities. If the user is using one of the
[instrumentation
libraries](../extending-the-sdk/README.md#instrumentation-library), it may
provide these functionalities automatically. Please refer to the respective
documentation for guidance.

## User-handled Exception

The term `User-handled Exception` is used to describe exceptions that are
handled by the application, as shown in the below sample code.

```csharp
try
{
    Func();
}
catch (SomeException ex)
{
    DoSomething();
}
catch (Exception ex)
{
    DoSomethingElse();
    throw;
}
```

OpenTelemetry .NET provides several options to report Exceptions in `Activity`.
It varies from the most basic option of setting `Status`, to fully recording the
`Exception` itself to activity.

### Option 1 - Set Activity Status manually

The most basic option is to set Activity status to Error to indicate that an
Exception has occurred.

While using `Activity` API, the common pattern would be:

```csharp
using (var activity = MyActivitySource.StartActivity("Foo"))
{
    try
    {
        Func();
    }
    catch (SomeException ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error);
        DoSomething();
    }
    catch (Exception ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error);
        throw;
    }
}
```

### Option 2 - Set Activity Status using SetErrorStatusOnException feature

The approach described in Option 1 could become hard to manage if there are
deeply nested `Activity` objects, or there are activities created in a 3rd party
library.

The following configuration will automatically detect exception and set the
activity status to `Error`:

```csharp
Sdk.CreateTracerProviderBuilder()
    .SetErrorStatusOnException()
    // ...
```

A complete example can be found [here](./Program.cs).

Note: this feature is platform dependent as it relies on
[System.Runtime.InteropServices.Marshal.GetExceptionPointers](https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.marshal.getexceptionpointers).

#### Caught exceptions in async code

This feature can set the status to `Error` on an activity whose code caught
the exception, when the exception came from an awaited call. In the
following example, `Foo` can end with status `Error` even though
`SomeException` is handled:

```csharp
using (var activity = MyActivitySource.StartActivity("Foo"))
{
    try
    {
        await FuncAsync(); // throws SomeException after its first await
    }
    catch (SomeException ex)
    {
        DoSomething();
    }
}
```

When an awaited method throws, the compiler-generated code catches the
exception and completes the `Task` from inside that catch block. The code
after the `await` (here, the `catch` block and the end of the `using` block)
often runs right away on the same thread, while that exception is still
being handled. `GetExceptionPointers` then reports that exception, and the
feature cannot tell it apart from an exception leaving the `using` block.
See [#2753](https://github.com/open-telemetry/opentelemetry-dotnet/issues/2753)
for details.

If this affects you, either:

* Add `await Task.Yield();` after the `catch` block, before the activity
  ends. The rest of the method then runs outside that catch block. If there
  is a synchronization context (for example, in a UI app), the rest of the
  method resumes on it rather than on a thread pool thread.
* Turn off `SetErrorStatusOnException` and set the status yourself, as in
  [Option 1](#option-1---set-activity-status-manually). The feature applies to
  the whole `TracerProvider`, so Option 1 on one code path is not enough
  while it is on.

Setting the status to `Ok` in the `catch` block, with
`activity.SetStatus(ActivityStatusCode.Ok)` or the older
`activity.SetStatus(Status.Ok)`, does not help, because the feature replaces
the status with `Error` when the activity ends.

### Option 3 - Set Activity Status with Error description

While convenient, the `SetErrorStatusOnException` feature only sets the activity
status to Error and nothing more. It is sometimes desirable to store the
exception message as the Status description. The following code shows how to do
that:

```csharp
using (var activity = MyActivitySource.StartActivity("Foo"))
{
    try
    {
        Func();
    }
    catch (SomeException ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
    }
}
```

### Option 4 - Use Activity.AddException

Both options 1 and 2 above showed the most basic reporting of Exception, by
leveraging Activity status. Neither of the approach actually records the
Exception itself to do more richer debugging. `Activity.AddException()`
allows the exception to be stored in the Activity as ActivityEvent as per
[OpenTelemetry
convention](https://github.com/open-telemetry/semantic-conventions/blob/main/docs/exceptions/exceptions-spans.md),
as shown below:

```csharp
using (var activity = MyActivitySource.StartActivity("Foo"))
{
    try
    {
        Func();
    }
    catch (SomeException ex)
    {
        activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
        activity?.AddException(ex);
    }
}
```

## Unhandled Exception

The term `Unhandled Exception` is used to describe exceptions that are not
handled by the application. When an unhandled exception happened, the behavior
will depend on the presence of a debugger:

* If there is no debugger, the exception will normally crash the process or
  terminate the thread.
* If a debugger is attached, the debugger will be notified that an unhandled
  exception happened.
* In case a postmortem debugger is configured, the postmortem debugger will be
  activated and normally it will collect a crash dump.

It might be useful to automatically capture the unhandled exceptions, travel
through the unfinished activities and export them for troubleshooting. Here goes
one possible way of doing this:

> [!CAUTION]
> Use `AppDomain.UnhandledException` with caution. A throw in the
handler puts the process into an unrecoverable state.

```csharp
using System;
using System.Diagnostics;
using OpenTelemetry;
using OpenTelemetry.Trace;

public class Program
{
    private static readonly ActivitySource MyActivitySource = new ActivitySource("MyCompany.MyProduct.MyLibrary");

    public static void Main()
    {
        AppDomain.CurrentDomain.UnhandledException += UnhandledExceptionHandler;

        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .SetSampler(new AlwaysOnSampler())
            .SetErrorStatusOnException()
            .AddConsoleExporter()
            .Build();

        using (MyActivitySource.StartActivity("Foo"))
        {
            using (MyActivitySource.StartActivity("Bar"))
            {
                throw new Exception("Oops!");
            }
        }
    }

    private static void UnhandledExceptionHandler(object source, UnhandledExceptionEventArgs args)
    {
        var ex = (Exception)args.ExceptionObject;

        var activity = Activity.Current;

        while (activity != null)
        {
            activity.AddException(ex);
            activity.Dispose();
            activity = activity.Parent;
        }
    }
}
```
