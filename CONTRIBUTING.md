# Contributing

## Report a bug or requesting feature

Reporting bugs is an important contribution. Please make sure to include:

- expected and actual behavior.
- dotnet version that application is compiled on and running with (it may be
  different - for instance target framework was set to .NET 4.6 for
  compilation, but application is running on .NET 4.7.3).
- exception call stack and other artifacts.
- if possible - repro application and steps to reproduce.

## How to contribute

### Before you start

Please read project contribution
[guide](https://github.com/open-telemetry/community/blob/master/CONTRIBUTING.md)
for general practices for OpenTelemetry project.

### Fork

In the interest of keeping this repository clean and manageable, you should work from a fork. To create a fork, click the 'Fork' button at the top of the repository, then clone the fork locally using `git clone git@github.com:USERNAME/opentelemetry-dotnet.git`.

You should also add this repository as an "upstream" repo to your local copy, in order to keep it up to date. You can add this as a remote like so:
```
git remote add upstream https://github.com/open-telemetry/opentelemetry-dotnet.git

#verify that the upstream exists
git remote -v
```

To update your fork, fetch the upstream repo's branches and commits, then merge your master with upstream's master:
```
git fetch upstream
git checkout master
git merge upstream/master
```

Remember to always work in a branch of your local copy, as you might otherwise have to contend with conflicts in master.

### Build

You can use Visual Studio 2017+ or VS Code to contribute. Just open root folder
or `OpenTelemetry.sln` in your editor and follow normal development process.

To build from command line you need `dotnet` version `2.0+`.

``` sh
dotnet build OpenTelemetry.sln
```

### Test

You can use Visual Studio 2017 or VS Code to test your contribution. Open root
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