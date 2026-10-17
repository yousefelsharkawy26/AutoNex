# Milestone 2: Test Framework Integration

## Goals
- Integrate with xUnit v4.0.2 as the primary test framework
- Add Reqnroll bindings for BDD scenarios
- Set up FluentAssertions for readable assertions
- Configure mocking framework (Moq or NSubstitute)
- Implement test data builder foundations
- Set up coverage collection and reporting

## Tasks
1. [x] Add xUnit dependencies to test projects
2. [x] Configure xUnit custom test fixtures using BaseTestFixture
3. [x] Add Reqnroll dependencies and create feature file templates
4. [x] Implement Reqnroll step definitions that interact with IDriver
5. [x] Add FluentAssertions dependencies and create custom assertions if needed
6. [x] Add mocking framework (Moq recommended) dependencies
7. [x] Create mocking helpers for common scenarios
8. [x] Implement test data builder pattern for consistent test data
9. [x] Set up coverlet for code coverage collection
10. [x] Set up ReportGenerator for coverage report generation
11. [x] Create sample tests demonstrating the integration
12. [ ] Configure test execution settings for parallelism and timeout handling

## Dependencies Added
- xunit 4.0.2
- xunit.runner.visualstudio
- Reqnroll 3.3.4
- FluentAssertions (latest)
- Moq 4.21.0
- coverlet.collector 10.1.0
- ReportGenerator 5.5.11

## Integration Points
- BaseTestFixture serves as the base for xUnit test classes
- IDriver is injected into test classes via constructors or properties
- WaitHelper is used in test steps for UI synchronization
- ScreenshotHelper is used for validation steps in BDD scenarios
- TestResult is enhanced to capture framework-specific information

## Completed Features
- ✅ xUnit test project configured with references to core library
- ✅ Reqnroll integrated for BDD scenarios with sample feature file and step definitions
- ✅ FluentAssertions added for readable assertions
- ✅ Moq configured for mocking dependencies
- ✅ Test data builder pattern implemented with UserBuilder example
- ✅ Coverage collection and reporting tools set up
- ✅ Sample tests demonstrating integration with core library components
- ✅ Guard class made public for test accessibility
- ✅ Test data builder made accessible with public With* methods
- ✅ BaseTestFixture refactored to work with xUnit (removed IClassFixture issues)

## Remaining Work
- [ ] Configure test execution settings for parallelism and timeout handling in xUnit.runner.settings.json
- [ ] Address platform-specific test failure (DriverFactory_CanCreateFlauiDriver) - expected to fail on Linux due to missing Windows Accessibility assembly
- [ ] Create additional test data builders for other common test objects
- [ ] Add more comprehensive Reqnroll scenarios
- [ ] Implement custom FluentAssertions extensions if needed
