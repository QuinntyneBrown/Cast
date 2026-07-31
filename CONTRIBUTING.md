# Contributing to Cast

Thanks for your interest in contributing! This document explains how to get set up,
the workflow we follow, and what we expect from a pull request.

> Cast is in early development, so the architecture is still taking shape. If you're
> planning a substantial change, please open an issue first to discuss the approach.

## Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later
- Git

## Getting started

Fork the repository, then clone your fork:

```pwsh
git clone https://github.com/<your-username>/cast.git
cd cast
dotnet build
dotnet test
```

If the build and tests pass, you're ready to make changes.

## Development workflow

1. **Create a branch** off `main` for your change:

   ```pwsh
   git checkout -b feature/short-description
   ```

2. **Make your change.** Keep it focused — one logical change per pull request.

3. **Add or update tests.** Reusable engine behaviour belongs in `tests/Cast.Core.Tests/`;
   command, workflow, and adapter behaviour belongs in `tests/Cast.Cli.Tests/`. Run them with:

   ```pwsh
   dotnet test
   ```

   Run a single test while iterating:

   ```pwsh
   dotnet test --filter "FullyQualifiedName~Cast.Cli.Tests.UnitTest1.Test1"
   ```

4. **Build cleanly.** Ensure `dotnet build` produces no new warnings.

## Coding guidelines

- Target **.NET 10**; nullable reference types and implicit usings are enabled — keep
  them satisfied rather than suppressing them.
- Match the existing code style and naming conventions in the surrounding files.
- Prefer small, readable methods and meaningful names over comments that explain
  non-obvious code.

## Commit messages

Write clear, imperative commit messages (e.g. "Add diagram scaffolding command").
Reference related issues where relevant (e.g. `Fixes #12`).

## Opening a pull request

1. Push your branch to your fork and open a pull request against `main`.
2. Describe **what** the change does and **why**.
3. Confirm that `dotnet build` and `dotnet test` pass.
4. Be responsive to review feedback — discussion is part of the process.

## Reporting bugs and requesting features

Use GitHub Issues. For bugs, include:

- What you expected to happen and what actually happened
- Steps to reproduce
- Your .NET SDK version (`dotnet --version`) and operating system

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
