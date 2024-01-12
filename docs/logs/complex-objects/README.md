# Logging with Complex Objects

In the [Getting Started with OpenTelemetry .NET Logs in 5 Minutes - Console
Application](../getting-started-console/README.md) tutorial, we've learned how
to log primitive data types. In this tutorial, we'll learn how to log complex
objects.

Complex objects logging was introduced in .NET 8.0 via
[LogPropertiesAttribute](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.logpropertiesattribute).
This attribute and the corresponding code generation logic are provided by an
extension package called
[Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions/).

> [!NOTE]
> Although `Microsoft.Extensions.Telemetry.Abstractions` was introduced in .NET
8.0, it supports previous versions of the target framework (e.g. .NET 6.0).
Refer to the [compatible target
frameworks](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions/#supportedframeworks-body-tab)
for more information.

First, complete the [getting started](../getting-started-console/README.md)
tutorial, then install the
[Microsoft.Extensions.Telemetry.Abstractions](https://www.nuget.org/packages/Microsoft.Extensions.Telemetry.Abstractions/)
package:

```sh
dotnet add package Microsoft.Extensions.Telemetry.Abstractions
```

Define a new complex data type, as shown in [FoodRecallNotice.cs](./FoodRecallNotice.cs):

```csharp
public class FoodRecallNotice
{
    public string? BrandName { get; set; }

    public string? ProductDescription { get; set; }

    public string? ProductType { get; set; }

    public string? RecallReasonDescription { get; set; }

    public string? CompanyName { get; set; }
}
```

Update the `Program.cs` file with the code from [Program.cs](./Program.cs). Note
that the following code is added which uses the `LogPropertiesAttribute` to log
the `FoodRecallNotice` object:

```csharp
public static partial class ApplicationLogs
{
    [LoggerMessage(LogLevel.Critical)]
    public static partial void FoodRecallNotice(
        this ILogger logger,
        [LogProperties(OmitReferenceName = true)] FoodRecallNotice foodRecallNotice);
}
```

The following code is used to create a `FoodRecallNotice` object and log it:

```csharp
var foodRecallNotice = new FoodRecallNotice
{
    BrandName = "Contoso",
    ProductDescription = "Salads",
    ProductType = "Food & Beverages",
    RecallReasonDescription = "due to a possible health risk from Listeria monocytogenes",
    CompanyName = "Contoso Fresh Vegetables, Inc.",
};

logger.FoodRecallNotice(foodRecallNotice);
```

Run the application again (using `dotnet run`) and you should see the log output
on the console.

```text
LogRecord.Timestamp:               2024-01-12T19:01:16.0604084Z
LogRecord.CategoryName:            Program
LogRecord.Severity:                Fatal
LogRecord.SeverityText:            Critical
LogRecord.FormattedMessage:
LogRecord.Body:
LogRecord.Attributes (Key:Value):
    CompanyName: Contoso Fresh Vegetables, Inc.
    RecallReasonDescription: due to a possible health risk from Listeria monocytogenes
    ProductType: Food & Beverages
    ProductDescription: Salads
    BrandName: Contoso
```

> [!NOTE]
> In this tutorial we used
[LogPropertiesAttribute.OmitReferenceName](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.logpropertiesattribute.omitreferencename)
which changes the style of attribute names. There are more options available,
check out the [learn more](#learn-more) section for more information.

## Learn more

* [Microsoft.Extensions.Logging.LogPropertiesAttribute](https://learn.microsoft.com/dotnet/api/microsoft.extensions.logging.logpropertiesattribute)
* [Microsoft.Extensions.Telemetry.Abstractions](https://github.com/dotnet/extensions/blob/main/src/Libraries/Microsoft.Extensions.Telemetry.Abstractions/README.md)
* [Strong-type support feature
  request](https://github.com/dotnet/runtime/issues/61947)
* [LogPropertiesAttribute design
  proposal](https://github.com/dotnet/runtime/issues/81730)
