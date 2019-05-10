# Contributing

## Report a bug or requesting feature

Reporting bug is an important contribution. Please make sure to include:

- expected and actual behavior.
- dotnet version that application is compiled on and running with (it may be
  different - for instance target framework was set to .NET 4.6 for
  compilaiton, but applicaiton is running on .NET 4.7.3).
- exception call stack and other artifacts.
- if possible - reporo application and steps to reproduce.

## How to contribute

### Before started

In order to protect both you and ourselves, you will need to sign the
[Contributor License Agreement](https://cla.developers.google.com/clas).

### Build

You can use Visual Studio 2017 or VS code to contribute. Just open root folder
or `OpenTelemetry.sln` in your editor and follow normal development process.

To build from command line you need `dotnet` version `2.0+`.

``` sh
dotnet build OpenTelemetry.sln
```

### Test

You can use Visual Studio 2017 or VS code to test your contribution. Open root
folder or `OpenTelemetry.sln` in your editor and follow normal development
process.

To test from command line you need `dotnet` version `2.0+`.

``` sh
dotnet test OpenTelemetry.sln
```

### Proposing changes

Create a Pull Request with your changes. Please add any user-visible changes to
`CHANGELOG.md`. The continuous integration build will run the tests and static
analysis. It will also check that the pull request branch has no merge commits.
When the changes are accepted, they will be merged or cherry-picked by an
OpenTelemetry repository maintainers.