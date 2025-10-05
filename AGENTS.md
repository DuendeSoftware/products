# Overview
This file contains instructions for working with the Duende Software products monorepo.

The products monorepo is a git repository that contains the publicly available source code for all Duende Software
products. It includes:
- IdentityServer, a .NET SDK for building OpenID Connect and OAuth 2.0 authorization servers and token services.
- BFF, a .NET SDK for implementing the Backend For Frontend (BFF) security pattern.
- Extensions for ASP.NET, which adds support for advanced security features to ASP.NET applications.
- .NET templates to get started with our products.

## Conventions of this file
- Run commands from the root of the repository unless a section explicitly states otherwise.

# Repository-wide Instructions

## Security & network
Never exfiltrate secrets or credentials. Do not make external network calls (HTTP, git push to remote, package publishes) without explicit human approval. If a change requires network access (new package, external API), stop and ask for permission and the required credentials or CI-side steps.

## Published packages
Each of the products in the repository is published as a collection of NuGet packages. The repository contains both projects that are packaged and support projects, such as tests and developer tools.

The projects that are published have different rules and conventions than those that are not (see below). The published projects are the projects in the `bff/src`, `/identity-server/src`, and  `/aspnetcore-authentication-jwtbearer/src` subdirectories.

## SDK Versions
The published packages and their tests should multi-target all the versions of the .NET SDK supported by Microsoft.

Other projects should singly target the latest version of the .NET SDK.

Use conditional compilation when differences in the SDK require different approaches. Do so when necessary, but defer changes when possible. For example, if a new version of the SDK deprecates a method in favor of a new replacement, do not add conditional compilation to use the new replacement. Instead, wait until we no longer support the older SDK version to make the change.

Changing target frameworks requires an explicit request from a human.

## Dependencies
Use central package management via Directory.Packages.props. Do not change Directory.Packages.props without explicit human approval.

The Directory.Packages.props file sets different versions of dependencies for different target frameworks using conditional MSBuild properties.

To request new dependencies, stop making code changes, describe the new dependency, explain it is necessary, and ask for explicit approval from a human. Note that new dependencies will generally not be approved for the projects that we publish as NuGet packages without a very strong reason.

### Version Update Policies
Dependency versions should be as relaxed as possible. Do not update to the latest patch versions of dependencies. Instead, chose the lowest patch version that the lowest patch version of a dependency that we can without depending on a version that contains a security vulnerability either directly or through transitive dependencies.

The Microsoft.IdentityModel.* and System.IdentityModel.* dependencies are especially problematic because they have shown a historical willingness to introduce breaking changes without following semver. Both our SDK packages and other packages that are commonly used with our SDK, such as Microsoft.AspNetCore.Authentication.OpenIdConnect, depend on these packages. We are careful to align our versions of these dependencies with the version of the dependency taken by the latest version of  ASP.NET OIDC authentication handler.

### Transitive Dependencies
If a transitive dependency contains a security issue, the preferred solution is to upgrade the upstream dependency to a version that also updates the transitive dependency. If no such version of the upstream dependency exists, add an explicit dependency on the transitive dependency and note with comment in the project file and Directory.Build.props file that the explicit transitive dependency has been added to prevent a depending on a vulnerable package transitively, and that this can be removed if the upstream dependency is updated to no longer depend on the vulnerable package.

## Breaking Changes
Breaking changes to public APIs require explicit human approval.

## Development Environment Verification

### Required SDK Version
The repository requires the .NET SDK version specified in `global.json`. Verify your environment:
```bash
dotnet --version
```
The SDK supports rollForward to newer versions, but using the specified version ensures consistency.

### Repository Setup Verification
Before making changes, verify the repository is in a buildable state:

```bash
# Verify you're in the repository root
ls global.json products.slnx

# Restore dependencies (required before first build)
dotnet restore

# Verify basic build works
dotnet build products.slnx
```

### Solution File Usage
- Use **products.slnx** for repository-wide operations
- Use **{product}/{product}.slnf** for product-specific work (e.g., `identity-server/identity-server.slnf`)
- Product-specific solution filters are faster for focused development

### Build Configuration
- **Debug builds**: `dotnet build` (default, faster for development)
- **Release builds**: `dotnet build -c release` (includes full code analysis, required before PR submission)

### Common Environment Issues
- **SDK version mismatch**: If build fails, check `dotnet --version` matches global.json requirements
- **Dependency conflicts**: Run `dotnet restore` if you see package-related errors
- **Code analysis warnings**: Release builds treat warnings as errors; Debug builds are more permissive

### Environment Health Check
Run these commands to verify your environment is ready:
```bash
# 1. Check SDK version
dotnet --version

# 2. Restore all dependencies
dotnet restore

# 3. Build repository (should complete without errors)
dotnet build products.slnx
```

If any of these commands fail, resolve the issues before proceeding with development work.

## Project and File Naming Conventions

### Published Project Names
Projects that are published as NuGet packages follow the `Duende.{Product}.{Feature}` pattern:
- **IdentityServer**: `Duende.IdentityServer`, `Duende.IdentityServer.EntityFramework`, `Duende.IdentityServer.Storage`, etc.
- **BFF**: `Duende.BFF`, `Duende.BFF.Blazor`, `Duende.BFF.EntityFramework`, etc.
- **JWT Bearer**: `Duende.AspNetCore.Authentication.JwtBearer`

### Test Project Names
Test projects follow these patterns:
- **Unit tests**: `{Product}.UnitTests` (e.g., `IdentityServer.UnitTests`, `Bff.Tests`)
- **Integration tests**: `{Product}.IntegrationTests` (e.g., `IdentityServer.IntegrationTests`)
- **End-to-end tests**: `{Product}.EndToEndTests` (e.g., `IdentityServer.EndToEndTests`)
- **Feature-specific tests**: `{Product}.{Feature}.Tests` (e.g., `Bff.EntityFramework.Tests`, `Bff.Blazor.UnitTests`)

### File Naming Conventions
- **Interfaces**: Start with `I` followed by PascalCase (e.g., `ITokenService.cs`, `IClaimsService.cs`)
- **Default implementations**: Prefix interface name with `Default` (e.g., `DefaultTokenService.cs`, `DefaultClaimsService.cs`)
- **Service implementations**: Use descriptive names ending in the service type (e.g., `DistributedDeviceFlowThrottlingService.cs`)
- **Models**: Use PascalCase descriptive names (e.g., `DeserializedPushedAuthorizationRequest.cs`)
- **Test files**: Match the class or feature being tested, adding `Tests` suffix (e.g., `PkceTests.cs`, `ConsentTests.cs`)

### Folder Structure Conventions
- Group related files in folders matching their namespace
- Place default implementations in a `Default/` subfolder when there are multiple implementations
- Organize test files to mirror the source code structure they're testing

## Style and Coding conventions
Follow the style rules in `.editorconfig`. Do not bypass or reformat contrary to its rules. The project is configured to treat warnings as errors. Following editorconfig rules is mandatory.

Run `dotnet format` before each commit to ensure style consistency.

Run `dotnet build -c release` before submitting a pull request to run the release build, which includes more code analysis checks.

If a human has specifically requested a different formatting for a region (and a rationale is recorded in the PR), agents should not automatically revert that change without human review.

### File Header Requirements
All .cs files must include the following copyright header:
```
// Copyright (c) Duende Software. All rights reserved.
// See LICENSE in the project root for license information.
```
When creating a new file, begin with this header.

## Tests
Write tests for all new features and bug fixes.

### Test-driven development
Before fixing a bug or implementing a new feature:
1. Write tests that verify the fix or new functionality.
2. Run the tests with the expectation that they should fail.
3. If the tests succeeded without changes to the implementation, change the new tests so that they fail.
4. Once you have tests that fail, commit the failings tests by themselves before proceeding to implementation.
5. Now proceed to the implementation, making the smallest change that will cause the tests to pass.
Failing tests provoke a change to the implementation, and we only make a change when we see a failure. To prevent false positives, we must always verify that without our changes, the test would fail.

### Test style
There is no need to strictly follow the arrange-act-assert pattern in tests, though it can be helpful. Do not include "arrange-act-assert" comments in tests.

Use shouldly for assertions.

Use XUnit for test projects.

#### Test Categories/Traits
All test classes must define a category using the `[Trait("Category", Category)]` attribute on test methods:
1. Declare a private const string at the top of each test class:
   ```csharp
   private const string Category = "YourCategoryName";
   ```
2. Apply the trait to each test method:
   ```csharp
   [Fact]
   [Trait("Category", Category)]
   public void your_test_method()
   ```

**Category naming conventions:**
- For conformance tests: `"Conformance.{Area}.{TestClassName}"` (e.g., `"Conformance.Basic.CodeFlowTests"`)
- For validation tests: descriptive names like `"TokenRequest Validation - RefreshToken - Invalid"`
- For feature tests: use the feature area name (e.g., `"PKCE"`, `"Local API Integration"`)
- For extensibility tests: use the class name (e.g., `"CustomClaimsServiceTests"`)

#### Test File Placement
- **Unit tests**: `/identity-server/test/IdentityServer.UnitTests/` - organized by feature area matching the main source structure
- **Integration tests**: `/identity-server/test/IdentityServer.IntegrationTests/` - organized by functional area (Endpoints, Conformance, Extensibility, etc.)
- **End-to-end tests**: `/identity-server/test/IdentityServer.EndToEndTests/` - for full system tests

#### Test Names
Use descriptive, lowercase method names with underscores:
- **Good**: `expired_par_requests_should_fail()`, `duplicate_values_should_throw()`, `custom_should_be_allowed()`
- **Pattern**: `{condition}_{should}_{expected_outcome}()` or `{action}_{expected_outcome}()`
- Avoid "Test" suffix in method names
- Be specific about the scenario being tested

## Git and GitHub
- The `main` branch represents the next stable release.
- All changes should be made on a feature branch. Never work directly on the `main` branch.
- Feature branches are typically based on the `main` branch and merged back into `main` when complete.
- Feature branches should be rebased onto the latest commit in `main` before merging.
- The naming convention for feature branches is {user-initials}/snake-case-name-of-feature. Agents, AI tools, Bots, etc should use "bot" as the user-initials. For example, `bot/null-ref-in-cleanup-job`.

### Pull Requests
- Use pull requests to propose changes and conduct code reviews.
- `dotnet format` should have been run before making each commit.
- Run `dotnet build -c release` and ensure there are no warnings or errors.
- Create a pull request with your changes.
- Label the pull request with the appropriate product-specific labels. If there are no user-facing changes, also add the label `internal`. The product-specific labels are `area/products/identity-server`, `area/products/bff`, `area/products/jwtbearer` and `area/products/templates`.


### CI/CD Pipeline
The CI/CD pipelines are automatically generated by the project in `.github/workflow-gen/` and should not be manually edited.

#### Pull Request Checks
Every pull request triggers multiple validation jobs:

**1. Practices Check** (`practices-check-pr.yml`)
- Validates PR title matches branch name conventions
- Ensures proper labeling (product-specific labels required)
- Checks for appropriate `internal` label when no user-facing changes

**2. Product-Specific CI** (e.g., `identity-server-ci.yml`)
- **Verify Formatting**: Runs `dotnet format --verify-no-changes` to ensure code follows `.editorconfig` rules
- **Build and Unit Tests**: Compiles in Release mode and runs unit/integration tests (`IdentityServer.UnitTests`, `IdentityServer.IntegrationTests`)
- **End-to-End Tests**: Runs Playwright browser tests (`IdentityServer.EndToEndTests`)
- **CodeQL Analysis**: Static security analysis for vulnerability detection
- **Pack and Sign**: Creates NuGet packages (for validation, not publishing)

#### CI Trigger Conditions
CI runs when changes affect:
- Product-specific files (e.g., `identity-server/**`)
- Shared configuration (`.editorconfig`, `Directory.Packages.props`, `global.json`)
- Build properties (`src.props`, `test.props`)
- Workflow files themselves

#### Expected Agent Workflow
1. **Before PR**: Run `dotnet format` and `dotnet build -c release` locally
2. **PR Creation**: All CI checks must pass before merge
3. **CI Failures**: Address formatting, build, or test failures promptly
4. **Security Issues**: CodeQL findings require investigation and resolution

#### Common CI Failure Causes
- **Formatting violations**: Run `dotnet format` and commit changes
- **Build warnings**: Release builds treat warnings as errors
- **Test failures**: May indicate breaking changes or environmental issues
- **Missing labels**: Add appropriate `area/products/*` labels to PR

### Release Branches and Tags
- Release branches are created from `main` for each new version. They are named according to the convention releases/{product-abbreviation}/{major}.{minor}.x.
   - IdentityServer's product abbreviation is "is". For example, the 7.3.0 release of IdentityServer is represented by the `releases/is/7.3.x` branch. This branch would be used not just for 7.3.0, but also 7.3.1, 7.3.2, etc.
   - BFF's product abbreviation is "bff".
   - The product abbrevation for the JWT-Bearer extensions is "aaj".
- Each release is tagged. The tags are named according to the convention {product-abbreviation}-{version}, for example, is-7.3.0.
- Bug fixes should usually be made on a branch from the `main` branch. In rare cases, we make bug fix releases of a past release. In that case, the bug fix branch should be created from the release branch. Doing so is unusual and requires explicit human instruction.

# Repository Structure
The monorepo contains the following high-level folders:
- aspnetcore-authentication-jwtbearer: source code for ASP.NET authentication JWT-bearer extensions.
- bff: source code for the BFF product.
- identity-server: source code for the IdentityServer product.
- shared: shared libraries used across multiple products.
- templates: the build process for the .NET templates we publish. The individual products contain content that is packaged into a single template package.

## Shared Files
These files are located in the root of the repository and apply to all products:
- .editorconfig: style settings
- .gitignore: untracked files
- Directory.Packages.props: NuGet package versions are managed using central package management
- LICENSE: licensing terms
- README.md: landing page for the repository on GitHub
- global.json: .NET SDK version
- products.slnx: solution file that includes all projects in the repository, using the new .slnx format. Each product folder contains a solution filter (.slnf) that includes only the projects for that product.
- samples.props, src.props, templates.props, and test.props: shared msbuild properties for samples, source code, templates, and tests respectively

# IdentityServer

## Quickstart (local dev)
To build IdentityServer and run the tests, ensure the dotnet sdk required by global.json is installed and run:

```
 # ensure SDK from global.json
dotnet --info
dotnet restore
dotnet build identity-server/identity-server.slnf
# Run tests (--no-build is optional. Use it after a build for faster test runs)
dotnet test identity-server/identity-server.slnf --no-build
```


## IdentityServer Folder
This folder contains the source code for the IdentityServer product, which includes:
- `/identity-server/aspire`: Aspire hosts used by developers and tests.
- `/identity-server/clients`: Sample OAuth clients used by developers and tests. These are ASP.NET Core applications that make use of IdentityServer to obtain tokens and access APIs.
- `/identity-server/hosts`: Sample IdentityServer hosts used by developers and tests. These are ASP.NET Core applications that have IdentityServer installed in various configurations.
- `/identity-server/migrations`: Entity Framework migrations for the configuration and operational stores used by some of the hosts.
- `/identity-server/perf`: Performance tests.
- `/identity-server/src`: Projects that make up the IdentityServer product. Each project is published as a separate NuGet package.
- `/identity-server/test`: Unit and integration test projects.
- `/identity-server/CHANGELOG.md`: A changelog that describes what's new in IdentityServer. This file forms the basis of our release notes and should be updated as part of most pull requests.


## Common Tasks
- Run all IdentityServer tests:
```
dotnet test identity-server/identity-server.slnf --no-build
```
- Quick Build
```
dotnet build identity-server/identity-server.slnf
```
- Full build with complete code analysis:
```
dotnet build identity-server/identity-server.slnf -c release
```

## CHANGELOG.md
Update `/identity-server/CHANGELOG.md` with a summary of changes made. **Only include user-facing changes** - omit internal refactoring, build tool updates, CI/CD changes, and updates to test clients or hosts.

### Entry Categories
Organize entries under these section headings (use only sections that apply):

- **## Breaking Changes** - Changes that break backward compatibility
- **## Enhancements** - New features and improvements
- **## Bug Fixes** - Fixes for existing functionality
- **## Code Quality** - Minor improvements that don't change behavior

### Entry Format
Follow this format for each entry:
```markdown
- Brief description of the change by @githubusername
  - Optional detailed explanation with technical context
  - Additional bullet points for complex changes
```

### Examples of User-Facing Changes (INCLUDE):
- New public APIs, interfaces, or services
- Breaking changes to existing APIs
- New configuration options or settings
- Bug fixes that affect end-user behavior
- Performance improvements
- Security enhancements

### Examples of Internal Changes (EXCLUDE):
- Test infrastructure updates
- Build process changes
- CI/CD pipeline modifications
- Refactoring without behavioral changes
- Updates to sample clients or hosts
- Developer tooling improvements

### Version Formatting
New versions should be added at the top using this format:
```markdown
# 7.x.x
```

# BFF
TODO

# ASP.NET JWT Bearer Extensions
TODO

# Templates
TODO

