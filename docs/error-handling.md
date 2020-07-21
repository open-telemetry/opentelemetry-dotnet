# Error handling in Open Census C# SDK

Open Census is a library that will in many cases run in a context of customer
app performing non-essential from app business logic perspective operations.
Open Census SDK also can and will often be enabled via platform extensibility
mechanisms and potentially only enabled in runtime. Which makes the use of SDK
non-obvious for the end user and sometimes outside of the end user control.

This makes some unique requirements for Open Census error handling practices.

## Basic error handling principles

Open Census SDK must not throw or leak unhandled or user unhandled exceptions.

1. APIs must not throw or leak unhandled or user unhandled exceptions when the
   API is used incorrectly by the developer. Smart defaults should be used so
   that the SDK generally works.
2. SDK must not throw or leak unhandled or user unhandled exceptions for
   configuration errors.
3. SDK must not throw or leak unhandled or user unhandled exceptions for errors
   in their own operations. Examples: telemetry cannot be sent because the
   endpoint is down or location information is not available because device
   owner has disabled it.

## Guidance

1. In .NET 4.0 and above, catching all exceptions will not catch corrupted
   state exceptions (CSEs).
    - We want this behavior—don’t catch CSEs
    - This allows exceptions like stack overflow, access violation to flow through
    - More information, refer to [MSDN](http://msdn.microsoft.com/en-us/magazine/dd419661.aspx).
2. Every background operation callback, Task or Thread method should have a
   global `try{}catch` statement to ensure reliability of an app.
3. When catching all exceptions in other cases, reduce the scope of the `try` as
   much as possible.
4. In general, don't catch, filter, and rethrow
    - Catch all exceptions and log error
    - If you must rethrow use `throw;` not `throw ex;`. It will ensure
      original call stack is preserved.
5. Beware of any call to external callbacks or override-able interface. Expect
   them to throw.
