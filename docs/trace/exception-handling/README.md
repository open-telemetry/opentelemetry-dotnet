# Exception Handling

## User-handled Exception

The term `User-handled Exception` is used to describe exceptions that are
handled by the application.

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
    catch (Exception)
    {
        activity?.SetStatus(Status.Error);
        throw;
    }
}
```

The above approach could become hard to manage if there are deeply nested
`Activity` objects, or there are activities created in a 3rd party library.

The following configuration will automatically detect exception and set the
activity status to `Error`:

```csharp
Sdk.CreateTracerProviderBuilder(options => {
    options.SetErrorStatusOnException = true;
});
```

A complete example can be found [here](./Program.cs).

Note: this feature is platform dependent as it relies on
[System.Runtime.InteropServices.Marshal.GetExceptionPointers](https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.marshal.getexceptionpointers).

## Unhandled Exception

The term `Unhandled Exception` is used to describe exceptions that are not
handled by the application. When an unhandled exception happened, the behavior
will depend on the presence of a debugger:

* If there is no debugger, the exception will normally crash the process or
  terminate the thread.
* If a debugger is attached, the debugger will be notified that an unhandled
  exception happened.
* In case a postmortem debugger is configured, the postmortem debugger will be
  activited and normally it will collect a crash dump.

It might be useful to automatically capture the unhandled exceptions, travel
through the unfinished activities and export them for troubleshooting. Here goes
one possible way of doing this:

**WARNING:** Use `AppDomain.UnhandledException` with caution. A throw in the
handler puts the process into an unrecoverable state.

<!-- markdownlint-disable MD013 -->
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

        using var tracerProvider = Sdk.CreateTracerProviderBuilder(options =>
            {
                options.SetErrorStatusOnException = true;
            })
            .AddSource("MyCompany.MyProduct.MyLibrary")
            .SetSampler(new AlwaysOnSampler())
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
<!-- markdownlint-enable MD013 -->
