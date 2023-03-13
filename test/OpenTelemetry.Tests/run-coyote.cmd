cd c:\repos\opentelemetry-dotnet\test\OpenTelemetry.Tests
dotnet build OpenTelemetry.Tests.csproj
coyote rewrite OpenTelemetry.Tests.coyote.json
cd C:\repos\opentelemetry-dotnet\test\OpenTelemetry.Tests\bin\Debug\net7.0
dotnet test OpenTelemetry.Tests.dll --filter CheckIfBatchIsExportingOnQueueLimit_Coyote
cd c:\repos\opentelemetry-dotnet\test\OpenTelemetry.Tests
