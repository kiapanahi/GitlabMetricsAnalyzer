# Copilot Instructions

This project is intended to analyze, calculate and measure different KPIs for the engineering team of my company. I work as the VP of engineering at KuriousLabs and we want to have a better understanding of our engineering processes and how we can improve them.

## Objective

Build a C# application that collects developer productivity metrics from an on-premise GitLab instance via API and stores them in PostgreSQL for analysis.

## Core Commands

### Development Workflow
- **Run full application with the aspire CLI** (recommended): `aspire run`
- **Build functions only**: Available as VS Code task "build (functions)"
- **Run functions standalone**: Available as VS Code task with func host
- **Clean build**: Available as VS Code task "clean (functions)"

## Architecture Overview

### Technology Stack
- Use vertical slice architecture for every feature.
- **.NET 10** with C# latest features, file-scoped namespaces
- **.NET Aspire** for local orchestration and service discovery
- **Database**: PostgreSQL with EF Core
- **Serilog** for logging
- **Hangfire** for job scheduling
- Always use minimal APIs.


### External Integrations
- **GitLab API**: Issues, PRs, discussions data
- **Jira API**: Boards, projects and issues data
  - Refer to the official documentation at https://developer.atlassian.com/cloud/jira/platform/rest/v3/ for API details.
  - Jira that we use is on-premise and is hosted at "https://jira.tomanpay.net/"
  - License type is "Data Center".


## Project Structure
- **KuriousLabs.Management.KPIAnalysis.ApiService** - The main project that contains business logic and APIs
- **KuriousLabs.Management.KPIAnalysis.ServiceDefaults** - Aspire service defaults and telemetry
- **KuriousLabs.Management.KPIAnalysis.Tests** - Unit and integration tests

## Code Style
- Prefer async/await over direct Task handling
- When checking for nul in C# prefer to use `is null` or `is not null`
- Use nullable reference types
- Use var over explicit type declarations 
- Always implement IDisposable when dealing with event handlers or subscriptions
- Prefer using async/await for asynchronous operations
- Use latest C# features (e.g., records, pattern matching)
- Use consistent naming conventions (PascalCase for public members, camelCase for private members)
- Use meaningful names for variables, methods, and classes
- Use dependency injection for services and components
- Use interfaces for service contracts and put them in a unique file
- Use file scoped namespaces in C# and are PascalCased
- Always add namespace declarations to Blazor components matching their folder structure
- Organize using directives:
  - Put System namespaces first
  - Put Microsoft namespaces second
  - Put application namespaces last
  - Remove unused using directives
  - Sort using directives alphabetically within each group

## Component Structure
- Keep components small and focused
- Extract reusable logic into services
- Use cascading parameters sparingly
- Prefer component parameters over cascading values
- When interacting with GitLab API, refer to the official documentation at https://docs.gitlab.com/api/api_resources/

## Error Handling
- Use try-catch blocks in event handlers
- Implement proper error boundaries
- Display user-friendly error messages
- Log errors appropriately
- **Usage Limit Errors**: Check for JSON error responses with "USAGE_LIMIT_EXCEEDED" ErrorCode and display UsageLimitDialog instead of raw error messages

## Testing
- Write unit tests for complex component logic only if i ask for tests
- Test error scenarios
- Mock external dependencies
- Use XUnit for component testing
- Create tests in the KuriousLabs.Management.KPIAnalysis.Tests project

## Documentation
- Include usage examples in comments
- Document any non-obvious behavior
- Keep documentation up to date

## Security
- Always validate user input

## File Organization
- Keep related files together
- Use meaningful file names
- Follow consistent folder structure
- Group components by feature when possible

### Package Management
- Uses **Central Package Management** via `Directory.Packages.props`
- All projects target **.NET 10** with nullable reference types enabled

## Git
- Always write clear and concise commit messages
- Always follow the 50/72 rule for commit messages
- Use branches for new features and bug fixes
- Always create pull requests for code reviews
- Always review code before merging

## Versioning Policy
- Version format follows **MAJOR.MINOR.PATCH** (e.g., 15.2.3)
- Update version in `Directory.Build.props` for each PR merged to main:
  - **Features/New functionality**: Increment MAJOR version (e.g., 15.0.0 → 16.0.0)
  - **Bug fixes**: Increment MINOR version (e.g., 15.0.0 → 15.1.0)
  - **Patches/Code cleanup/Refactoring**: Increment PATCH version (e.g., 15.0.0 → 15.0.1)
- Every merged PR must include a version bump
- Version number provides traceability to feature implementations and fixes
