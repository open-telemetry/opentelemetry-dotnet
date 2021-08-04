# Contributing

Information on contributing to this repo is in the [Contributing
Guide](https://github.com/aspnet/Home/blob/master/CONTRIBUTING.md) in
the Home repo.

## Build and test

1. Open project in Visual Studio 2017+.
2. Build and compile run unit tests right from Visual Studio.

Command line:

```
dotnet build .\Microsoft.AspNet.TelemetryCorrelation.sln
dotnet test .\Microsoft.AspNet.TelemetryCorrelation.sln
dotnet pack .\Microsoft.AspNet.TelemetryCorrelation.sln
```

## Manual testing

Follow readme to install http module to your application.

Set `set PublicRelease=True` before build to produce delay-signed
assembly with the public key matching released version of assembly.