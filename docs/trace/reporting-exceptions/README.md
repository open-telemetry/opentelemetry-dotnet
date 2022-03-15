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
        activity?.SetStatus(Status.Error);
        DoSomething();
    }
    catch (Exception ex)
    {
        activity?.SetStatus(Status.Error);
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
        activity?.SetStatus(Status.Error.WithDescription(ex.message));
    }
}
```

### Option 4 - Use Activity.RecordException

Both options 1 and 2 above showed the most basic reporting of Exception, by
leveraging Activity status. Neither of the approach actually records the
Exception itself to do more richer debugging. `Activity.RecordException()`
allows the exception to be stored in the Activity as ActivityEvent as per
[OpenTelemetry
convention](https://github.com/open-telemetry/opentelemetry-specification/blob/main/specification/trace/semantic_conventions/exceptions.md),
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
        activity?.SetStatus(Status.Error.WithDescription(ex.message));
        activity?.RecordException(ex);
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

**WARNING:** Use `AppDomain.UnhandledException` with caution. A throw in the
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
            activity.RecordException(ex);
            activity.Dispose();
            activity = activity.Parent;
        }
    }
}
```
