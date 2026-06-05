# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

Early-stage scaffold. The repository currently contains the default .NET console and xUnit templates only — `src/Cast.Cli/Program.cs` is still `Console.WriteLine("Hello, World!")` and `tests/Cast.Cli.Tests/UnitTest1.cs` is an empty `[Fact]`. There are no commits yet. The solution name (`PlantUmlSeqScaffold`) suggests the intended purpose is generating/scaffolding PlantUML sequence diagrams, but no such code exists yet.

## Commands

All projects target **.NET 10** (`net10.0`) with `ImplicitUsings` and `Nullable` enabled.

```pwsh
dotnet build                              # build all projects
dotnet run --project src/Cast.Cli         # run the CLI
dotnet test                               # run all tests
```

Run a single test (xUnit via `dotnet test --filter`):

```pwsh
dotnet test --filter "FullyQualifiedName~Cast.Cli.Tests.UnitTest1.Test1"
dotnet test --filter "Name=Test1"         # by method name only
```

Note: there is no `.sln`/`.slnx` file present on disk, so `dotnet` commands resolve projects from the current directory tree. If a solution is added, prefer running commands against it.

## Layout

- `src/Cast.Cli/` — console application entry point (`Program.cs`, `OutputType=Exe`).
- `tests/Cast.Cli.Tests/` — xUnit test project (`xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`). The `Xunit` namespace is globally imported via a `<Using>` item, so test files don't need `using Xunit;`.
