cd c:\repos\opentelemetry-dotnet\test\OpenTelemetry.Tests
dotnet build OpenTelemetry.Tests.csproj --arch x86
coyote rewrite OpenTelemetry.Tests.coyote-x86.json
cd C:\repos\opentelemetry-dotnet\test\OpenTelemetry.Tests\bin\Debug\net7.0\win-x86
dotnet test OpenTelemetry.Tests.dll --filter CheckIfBatchIsExportingOnQueueLimit_Coyote
cd c:\repos\opentelemetry-dotnet\test\OpenTelemetry.Tests
