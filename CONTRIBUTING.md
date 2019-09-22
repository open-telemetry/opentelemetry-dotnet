# Contributing

## Report a bug or requesting feature

Reporting bugs is an important contribution. Please make sure to include:

- expected and actual behavior.
- dotnet version that application is compiled on and running with (it may be
  different - for instance target framework was set to .NET 4.6 for
  compilation, but application is running on .NET 4.7.2).
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

Please also see [GitHub workflow](https://github.com/open-telemetry/community/blob/master/CONTRIBUTING.md#github-workflow) section of general project contributing guide.

### Prerequisites

You can contribute to this project from a Windows, macOS or Linux machine. Requirements can very slightly:

In all platforms, the requirements are:

* Git client and command line tools. You may use Visual Studio to clone the repo, but we use [SourceLink](https://github.com/dotnet/sourcelink) to build and it needs git.
* .NET Core 2.1+

#### Windows

* Visual Studio 2017+, VS Code or JetBrains Rider
* .NET Framework 4.6+

#### macOS or Linux

* Visual Studio for Mac, VS Code or JetBrains Rider

Mono might be required by your IDE but is not required by this project.
This is because unit tests targeting .NET Framework (i.e: `net46`) are disabled outside of Windows.

### Build

Open `OpenTelemetry.sln` in your IDE of choice and follow normal development process.

To build from the command line you need `dotnet` version `2.1+`.

``` sh
dotnet build OpenTelemetry.sln
```

### Test

You can use any of the IDEs mentioned above to test your contribution. Open root
folder or `OpenTelemetry.sln` in your editor and follow normal development
process.

To test from command line you need `dotnet` version `2.0+`.

``` sh
dotnet test OpenTelemetry.sln
```

To see test coverage, run `dotnet test` from a console window and you will see the following output:

![image](https://user-images.githubusercontent.com/20248180/59361025-1e1e7980-8d29-11e9-8449-548caf0d7823.png)

Or, after running the tests, open the file `TestResults\Results\index.htm` in a browser.

### Coding style

This project includes a [`.editorconfig`](https://github.com/open-telemetry/opentelemetry-dotnet/blob/master/.editorconfig)
file which is supported by all the IDEs/editor mentioned above.
It works with the IDE/editor only and does not affect the actual build of the project.

This repository also includes a
[_test_, _prod_ and _prod.loose_ _stylecop ruleset_](https://github.com/open-telemetry/opentelemetry-dotnet/tree/master/build) files.

These files are used to configure the _StyleCop.Analyzers_ which runs during build. Breaking the rules will result in a broken build.

### Proposing changes

Create a Pull Request with your changes. Please add any user-visible changes to
`CHANGELOG.md`. The continuous integration build will run the tests and static
analysis. It will also check that the pull request branch has no merge commits.
When the changes are accepted, they will be merged or cherry-picked by an
OpenTelemetry repository maintainers.
