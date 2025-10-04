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

## Dependencies
This monorepo uses central package management via Directory.Packages.props. Do not change Directory.Packages.props without explicit human approval.

## Breaking Changes
Breaking changes to public APIs are only allowed judiciously and require explicit human approval.

## Style and Coding conventions
- Follow the style rules in `.editorconfig`. Do not bypass or reformat contrary to its rules.
- Run `dotnet format` before each commit to ensure style consistency.
- Run `dotnet build -c release` after making changes to run the release build, which includes more code analysis checks.
- If a human has specifically requested a different formatting for a region (and a rationale is recorded in the PR), agents should not automatically revert that change without human review.

## Tests
Write tests for all new features and bug fixes.

### Test-driven development
Before fixing a bug or implementing a new feature:
1. Write tests that verify the fix or new functionality.
2. Run the tests with the expectation that they should fail.
3. If the tests succeeded without changes to the implementation, change the new tests so that they fail.
4. Once you have tests that fail, commit them.
5. Now proceed to modify the implementation, making the smallest change that will cause the tests to pass.
Failing tests provoke a change to the implementation, and we only make a change when we see a failure. To prevent false positives, we must always verify that without our changes, the test would fail.

### Test style
- There is no need to strictly follow the arrange-act-assert pattern in tests, though can be helpful.
- Do not include "arrange-act-assert" comments in tests.
- Use shouldly for assertions.
- Use XUnit for test projects.

## PR Checklist
- `dotnet format` should have been run before making each commit.
- Run `dotnet -c release` and ensure there are no warnings or errors.
- Create a pull request with your changes.
- Label the pull request `products/identity-server`. If there are no user-facing changes, also add the label `internal`.

## Git
- The `main` branch represents the next stable release.
- Release branches are created from `main` for each new version. They are named according to the convention releases/{product-abbreviation}/{major}.{minor}.x.
   - IdentityServer's product abbreviation is "is". For example, the 7.3.0 release of IdentityServer is represented by the `releases/is/7.3.x` branch. This branch would be used not just for 7.3.0, but also 7.3.1, 7.3.2, etc.
   - BFF's product abbreviation is bff.
   - The product abbrevation for the Extensions for JWT-Bearer Authentication is aaj.
- Each release is tagged. The tags are named according to the convention {product-abbreviation}-{version}, for example, is-7.3.0.
- Feature branches should be based on the `main` branch and merged back into `main` when complete.
- Bug fixes should usually be made on a branch from the `main` branch. In rare cases, we make bug fix releases of a past release. If we desired that, we would make the bug fix branch from the release branch. Doing so is unusual and requires explicit human instruction.
- Use pull requests to propose changes and conduct code reviews.

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
- Update `/identity-server/CHANGELOG.md` with a summary of changes made. Internal changes, such as refactoring without a breaking change, updates to build tools and the CI/CD pipeline, and updates to clients or hosts should be omitted. `CHANGELOG.md` should only include user-facing changes. Imitate the style of the existing changelog entries in that file.
