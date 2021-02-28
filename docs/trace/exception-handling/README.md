# Exception Handling

## First Chance Exception

The term `First Chance Exception` is used to describe exceptions that are
handled by the application - whether in the user code or the framework.

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

The following configuration will automatically detect first chance exception and
automatically set the activity status to `Error`:

```csharp
Sdk.CreateTracerProviderBuilder(options => {
    options.SetErrorStatusOnUnhandledException = true;
});
```

A complete example can be found [here](./Program.cs).

Note: this feature is platform dependent as it relies on
[System.Runtime.InteropServices.Marshal.GetExceptionPointers](https://docs.microsoft.com/dotnet/api/system.runtime.interopservices.marshal.getexceptionpointers).

## Second Chance Exception

The term `Second Chance Exception` is used to describe exceptions that are not
handled by the application.

When a second chance exception happened, the behavior will depend on the
presence of a debugger:

* If there is no debugger, the exception will normally crash the process or
  terminate the thread.
* If a debugger is attached, the debugger will be notified that an "unhandled
  exception" happened.
* In case a postmortem debugger is configured, the postmortem debugger will be
  activited and normally it will collect a crash dump.

It might be useful to automatically capture the unhandled exceptions, travel
through the unfinished activities and export them for troubleshooting. One
possible way of doing this by using
[AppDomain.UnhandledException](https://docs.microsoft.com/dotnet/api/system.appdomain.unhandledexception)
can be found [here](./Program.cs).

Note: _handling unhandled exception is a very dangerous thing since the handler
itself could introduce exception, which would result in an unrecoverable
situation similar to [triple
fault](https://en.wikipedia.org/wiki/Triple_fault)_.
