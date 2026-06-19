# Build script tests

This directory contains [Pester](https://pester.dev/) tests for the PowerShell
scripts in `build/scripts`. These scripts are mostly used by the release
automation workflows and are not otherwise exercised by the build, so the tests
guard against changes that would only break later when other GitHub events occur.

## Running the tests

The tests require [PowerShell Core](https://github.com/PowerShell/PowerShell)
(`pwsh`) 7 or later.

To run the tests locally run the following command:

```pwsh
./build/scripts/tests/RunTests.ps1
```
