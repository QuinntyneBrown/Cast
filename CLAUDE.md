# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

`cast` is a working CLI that scaffolds initial PlantUML sequence diagrams. The `generate`
command turns command-line participants and messages into a `@startuml … @enduml` skeleton; the
`kinds` command lists the participant kinds; the `ng` command inspects an Angular `.ts` file and
renders a narrated diagram of how Angular injects dependencies into any `inject()`-using construct
(service, component, directive, pipe, interceptor, guard, resolver, or exported function), writing a
`.puml` beside the source by default (`--stdout` prints instead). The design follows SOLID with
`Microsoft.Extensions.DependencyInjection` and a one-command-per-file layout. Solution: `Cast.sln`.

## Commands

All projects target **.NET 8** (`net8.0`) so the tool runs on the .NET 8 runtime and later, with
`ImplicitUsings` and `Nullable` enabled. The CLI project sets `TreatWarningsAsErrors=true`, so the
build must stay warning-clean.

```pwsh
dotnet build Cast.sln                                         # build all projects
dotnet run --project src/Cast.Cli -- generate -p actor:User -p OS   # run the CLI
dotnet run --project src/Cast.Cli -- ng -s path/to/foo.service.ts   # writes foo.service.puml beside it
dotnet test Cast.sln                                          # run all tests
```

Run a single test (xUnit via `dotnet test --filter`):

```pwsh
dotnet test --filter "FullyQualifiedName~Cast.Cli.Tests.ParticipantParserTests"
dotnet test --filter "Name=Parse_BareName_DefaultsToParticipantKind"
```

## Architecture

The CLI is wired through dependency injection. `Program.cs` is the composition root: it builds a
`ServiceCollection` via `AddCast()`, resolves `RootCommandFactory`, and invokes the root command.

- `src/Cast.Cli/Program.cs` — composition root (`OutputType=Exe`, `AssemblyName=cast`).
- `src/Cast.Cli/Hosting/` — `ServiceCollectionExtensions.AddCast` (the single DI registration
  point), `RootCommandFactory` (assembles the root command from every registered `ICliCommand`),
  and `ExitCode`.
- `src/Cast.Cli/Commands/` — one `ICliCommand` per file (`GenerateCommand`, `ListKindsCommand`,
  `NgCommand`). A command maps parsed options to a request record and delegates to an orchestrator
  service; no `System.CommandLine` type leaks into the core.
- `src/Cast.Cli/Services/` — focused, interface-backed services: kind catalog, participant and
  message parsers, sample-flow generator, renderer (`ISequenceDiagramRenderer`), writer
  (`IDiagramWriter`), and the `IScaffoldService` orchestrator. The `ng` command adds its own set:
  `IAngularServiceParser` (a comment/string-aware scanner that extracts a consumer and its injected
  dependencies — no Node sidecar), `IAngularDiagramRenderer` (the narrated DI diagram),
  `ISourceFileReader` (the read-side I/O boundary, mirroring `IDiagramWriter`), and the
  `IAngularDiagramService` orchestrator.
- `src/Cast.Cli/Models/` — immutable records (`Participant`, `Message`, `SequenceDiagram`,
  `ScaffoldRequest`, `ParticipantKind`; plus `AngularService`, `AngularDependency`,
  `AngularDiagramRequest`, `ConsumerKind`, `DependencyKind` for the `ng` command).
- `src/Cast.Cli/Diagnostics/` — `DiagramFormatException` for user-facing input errors.

Conventions: the diagram goes to **stdout**, logs go to **stderr**. Adding a command means adding
an `ICliCommand` and registering it in `AddCast` — no central switchboard to edit.

## Tests

`tests/Cast.Cli.Tests/` — xUnit (`xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`). The
`Xunit` namespace is globally imported via a `<Using>` item, so test files don't need
`using Xunit;`. One test class per production type under test.
