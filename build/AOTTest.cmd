SETLOCAL
SETLOCAL ENABLEEXTENSIONS

dotnet publish ./test/OpenTelemetry.AotCompatibility.TestApp/OpenTelemetry.AotCompatibility.TestApp.csproj --self-contained -nodeReuse:false /p:UseSharedCompilation=false > myoutput.txt
