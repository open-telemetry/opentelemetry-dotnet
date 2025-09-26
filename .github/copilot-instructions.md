# OpenTelemetry .NET

Always reference these instructions first and fallback to search or bash commands only when you encounter unexpected information that does not match the info here.

OpenTelemetry .NET is the official .NET implementation of OpenTelemetry, providing comprehensive observability instrumentation for logs, metrics, and tracing across all .NET platforms including .NET Framework 4.6.2+, .NET Core, and modern .NET.

## Working Effectively

### Prerequisites and Setup

**CRITICAL**: This repository requires a specific .NET SDK version (specified in global.json). Install it before attempting any builds:

- Check the required version: `cat global.json` (look for the "version" field)
- Download and install the required .NET SDK version:
  ```bash
  # Install the latest stable .NET SDK from https://dotnet.microsoft.com/download
  # Or use your system's package manager
  # The SDK will automatically use the version specified in global.json
  ```
- Install markdownlint for markdown validation: `npm install -g markdownlint-cli`
- Verify setup: `dotnet --version` should show a compatible version that satisfies global.json

### Build and Test Commands

**NEVER CANCEL** long-running builds or tests. They may take 30+ minutes. Use appropriate timeouts and wait for completion.

**Core Build Commands:**
- `dotnet restore` - Restore NuGet packages (~2-5 minutes)
- `dotnet build` - Build entire solution (~10-20 minutes, NEVER CANCEL, timeout 45+ minutes)
- `dotnet test` - Run all tests (~15-30 minutes, NEVER CANCEL, timeout 60+ minutes)
- `dotnet build src/OpenTelemetry/OpenTelemetry.csproj` - Build core SDK only (~2-5 minutes)
- `dotnet test test/OpenTelemetry.Tests/OpenTelemetry.Tests.csproj` - Run core tests only (~5-10 minutes)

**Specialized Commands:**
- `dotnet format --verify-no-changes` - Check code formatting (runs fast)
- `markdownlint .` - Validate markdown files (runs fast)
- `dotnet pack` - Create NuGet packages (~10-15 minutes)

### Linting and Code Quality

Always run these before committing changes or the CI will fail:

1. **Code formatting**: `dotnet format --verify-no-changes`
2. **Markdown linting**: `markdownlint .`
3. Both tools respect .editorconfig and project-specific configurations

If formatting issues exist, fix them with: `dotnet format`

### Validation Scenarios

Always manually validate changes by running complete end-to-end scenarios:

1. **Basic SDK Validation**:
   ```bash
   cd examples/Console
   dotnet build
   dotnet run
   # Should output traces and metrics to console
   ```

2. **ASP.NET Core Integration**:
   ```bash
   cd examples/AspNetCore
   dotnet build
   dotnet run
   # Should start web server with OpenTelemetry instrumentation
   ```

3. **Core API Testing**:
   ```bash
   dotnet test test/OpenTelemetry.Api.Tests/OpenTelemetry.Api.Tests.csproj
   # Should pass all API contract tests
   ```

## Project Structure and Navigation

### Key Directories

- **`src/`** - Main source code for all packages
  - `src/OpenTelemetry/` - Core SDK implementation (most important)
  - `src/OpenTelemetry.Api/` - Public API surface (stable, breaking changes forbidden)
  - `src/OpenTelemetry.Exporter.*/` - Various exporters (Console, OTLP, Zipkin, Prometheus)
  - `src/OpenTelemetry.Extensions.*/` - Extension packages for hosting, propagators
  - `src/OpenTelemetry.Shims.*/` - Compatibility shims (e.g., OpenTracing)

- **`test/`** - All test projects
  - `test/OpenTelemetry.Tests/` - Core SDK tests (most comprehensive)
  - `test/OpenTelemetry.Api.Tests/` - API contract tests
  - `test/OpenTelemetry.Tests.Stress*/` - Stress testing applications
  - `test/Benchmarks/` - Performance benchmarks

- **`docs/`** - Documentation and runnable examples
  - `docs/logs/` - Logging documentation and examples
  - `docs/metrics/` - Metrics documentation and examples  
  - `docs/trace/` - Tracing documentation and examples

- **`examples/`** - Sample applications demonstrating usage
  - `examples/Console/` - Simple console app examples
  - `examples/AspNetCore/` - ASP.NET Core web app examples
  - `examples/GrpcService/` - gRPC service examples

- **`build/`** - Build configuration and MSBuild targets

### Important Files

- **`global.json`** - Specifies required .NET SDK version (check this file for current version)
- **`.github/workflows/ci.yml`** - Main CI pipeline configuration
- **`.editorconfig`** - Code style configuration (enforced by dotnet format)
- **`Directory.Packages.props`** - Centralized package version management
- **`OpenTelemetry.sln`** - Main solution file
- **`.markdownlint.yaml`** - Markdown linting configuration

### Frequently Used Projects

When making changes, these are the most commonly modified areas:

1. **Core API**: `src/OpenTelemetry.Api/` - Public contracts and interfaces (breaking changes forbidden)
2. **Core SDK**: `src/OpenTelemetry/` - Main implementation of traces, metrics, logs
3. **Console Exporter**: `src/OpenTelemetry.Exporter.Console/` - Simple debug output exporter
4. **OTLP Exporter**: `src/OpenTelemetry.Exporter.OpenTelemetryProtocol/` - Standard protocol exporter
5. **Core Tests**: `test/OpenTelemetry.Tests/` - Comprehensive test suite
6. **API Tests**: `test/OpenTelemetry.Api.Tests/` - API contract validation

## Known Issues and Workarounds

### Build Issues

- **Requires specific .NET SDK version**: Must use the .NET SDK version specified in global.json
- **Long build times**: Full solution build can take 20+ minutes, never cancel early
- **Package restore timeouts**: May take 5+ minutes on first run, be patient
- **Test timeouts**: Full test suite takes 30+ minutes, use targeted testing for faster feedback

### Platform-Specific Notes

- **Windows**: Supports all target frameworks including .NET Framework 4.6.2+
- **Linux/macOS**: .NET Framework tests are disabled, only .NET Core/.NET targets run
- **CI Environment**: Uses multiple OS and framework combinations

### Performance Considerations

- **Incremental builds**: Subsequent builds are faster after initial full build
- **Parallel execution**: dotnet build/test use multiple cores by default
- **Selective testing**: Use `--filter` with dotnet test for targeted test runs

## CI/CD Integration

The repository uses sophisticated CI workflows:

- **Markdown linting** via markdownlint-cli
- **Code formatting** via dotnet format  
- **Multi-platform testing** (Windows, Linux, ARM)
- **Multi-framework support** (.NET Framework 4.6.2, .NET 8.0, .NET 9.0)
- **Package validation** for public API compatibility
- **CodeQL security scanning**
- **Dependency vulnerability scanning**

### Before Committing Changes

Always ensure your changes pass:

1. `dotnet format --verify-no-changes` (fix with `dotnet format` if needed)
2. `markdownlint .` (fix markdown issues manually)
3. `dotnet build` (ensure no compilation errors)
4. `dotnet test test/OpenTelemetry.Tests/OpenTelemetry.Tests.csproj` (run core tests)
5. Manual validation with an example application

## Common Development Workflows

### Quick Status Check
```bash
# Verify environment setup
dotnet --version  # Should show a version compatible with global.json
markdownlint --version  # Should be installed

# Quick build validation  
dotnet restore
dotnet build src/OpenTelemetry/OpenTelemetry.csproj
```

### Full Development Cycle
```bash
# After making changes
dotnet format  # Fix any formatting issues
dotnet build src/OpenTelemetry/OpenTelemetry.csproj  # Build affected components
dotnet test test/OpenTelemetry.Tests/OpenTelemetry.Tests.csproj  # Run tests
markdownlint .  # Validate markdown if changed

# Validate with example
cd examples/Console
dotnet run  # Should demonstrate your changes working
```

### Adding New Features
```bash
# 1. Create/modify source in src/OpenTelemetry/ or appropriate project
# 2. Add/update tests in corresponding test/ project
# 3. Update documentation in docs/ if needed
# 4. Add example usage in examples/ if appropriate
# 5. Run full validation cycle above
```

## Public API Guidelines

**CRITICAL**: Never make breaking changes to public APIs in stable packages. Use these analyzers to validate:

- Microsoft.CodeAnalysis.PublicApiAnalyzers (enforced in build)
- Package validation (enforced in CI)
- API review process (required for changes)

For new public APIs:
1. Follow .NET API design guidelines
2. Add to PublicAPI.Shipped.txt or PublicAPI.Unshipped.txt as appropriate
3. Ensure comprehensive test coverage
4. Document in XML comments for IntelliSense

This repository is stable and production-ready across all three observability signals (Logs, Metrics, Traces) with comprehensive .NET ecosystem support.