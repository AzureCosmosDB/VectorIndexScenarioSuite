# CI Integration Tests

This repository now includes Continuous Integration (CI) tests that run automatically on GitHub Actions.

## Test Categories

Tests are organized into different categories:

### Unit Tests (`TestCategory=Unit`)
- **ScenarioParsingTests**: Tests for scenario name parsing and validation
- **BinaryDataTypeTests**: Tests for binary data type handling

### Integration Tests (`TestCategory=Integration`)
- **ConfigurationIntegrationTests**: Tests for configuration setup and JSON merging
- **ProgramIntegrationTests**: Tests for program initialization and scenario creation

### External Dependency Tests (`TestCategory=ExternalDependency`)
- **WikiCohereTest**: Tests that require Azure Cosmos DB connection (cloud or emulator)

## CI Workflow

The CI workflow (`.github/workflows/ci.yml`) includes:

1. **Cross-platform builds**: Tests run on both Ubuntu (Linux) and Windows
2. **Automated test execution**: Unit and integration tests run on every push/PR
3. **Dependency isolation**: External dependency tests are excluded from CI runs

## Running Tests Locally

### Run all CI-safe tests (Unit + Integration without external dependencies):
```bash
dotnet test VectorIndexScenarioSuite.Tests.csproj --filter "TestCategory=Unit|TestCategory=Integration&TestCategory!=ExternalDependency"
```

### Run only unit tests:
```bash
dotnet test VectorIndexScenarioSuite.Tests.csproj --filter "TestCategory=Unit"
```

### Run only integration tests (without external dependencies):
```bash
dotnet test VectorIndexScenarioSuite.Tests.csproj --filter "TestCategory=Integration&TestCategory!=ExternalDependency"
```

### Run external dependency tests (requires Cosmos DB setup):
```bash
dotnet test VectorIndexScenarioSuite.Tests.csproj --filter "TestCategory=ExternalDependency"
```

## Cross-Platform Compatibility

The project now supports cross-platform builds:
- Windows: Uses `win-x64` runtime by default
- Linux/macOS: Uses platform-agnostic runtime when `RuntimeIdentifier` is not specified
- CI environments automatically use the appropriate runtime

## Test Structure

- **VectorTestBase**: Base class providing common test utilities
- **Configuration helpers**: JSON merging and configuration setup utilities
- **Scenario validation**: Tests for all supported scenario types
- **Error handling**: Comprehensive exception testing for invalid inputs

The CI tests focus on business logic validation without requiring external services, ensuring reliable and fast CI execution.