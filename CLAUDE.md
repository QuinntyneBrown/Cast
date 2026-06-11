# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

`cast` is a working CLI that scaffolds initial PlantUML sequence diagrams. The `generate`
command turns command-line participants and messages into a `@startuml … @enduml` skeleton,
writing `cast.puml` in the current directory by default (`--stdout` prints instead); the
`kinds` command lists the participant kinds; the `ng` command inspects an Angular `.ts` file and
renders a narrated diagram of how Angular injects dependencies into any `inject()`-using construct
(service, component, directive, pipe, interceptor, guard, resolver, or exported function), writing a
`.puml` beside the source by default (`--stdout` prints instead); the `style` command retrofits the
house styling onto existing `.puml` sequence diagrams in place (one file, or a folder scanned
recursively), leaving non-sequence diagrams untouched. The design follows SOLID with
`Microsoft.Extensions.DependencyInjection` and a one-command-per-file layout. Solution: `Cast.sln`.

## Commands

All projects target **.NET 8** (`net8.0`) so the tool runs on the .NET 8 runtime and later, with
`ImplicitUsings` and `Nullable` enabled. The CLI project sets `TreatWarningsAsErrors=true`, so the
build must stay warning-clean.

```pwsh
dotnet build Cast.sln                                         # build all projects
dotnet run --project src/Cast.Cli -- generate -p actor:User -p OS   # writes cast.puml in the cwd
dotnet run --project src/Cast.Cli -- ng -s path/to/foo.service.ts   # writes foo.service.puml beside it
dotnet run --project src/Cast.Cli -- style docs/diagrams            # restyles sequence .puml files in place
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
  `NgCommand`, `StyleCommand`). A command maps parsed options to a request record and delegates to
  an orchestrator service; no `System.CommandLine` type leaks into the core.
- `src/Cast.Cli/Services/` — focused, interface-backed services: kind catalog, participant and
  message parsers, sample-flow generator, renderer (`ISequenceDiagramRenderer`), writer
  (`IDiagramWriter`), and the `IScaffoldService` orchestrator. The `ng` command adds its own set:
  `IAngularServiceParser` (a comment/string-aware scanner that extracts a consumer and its injected
  dependencies — no Node sidecar), `IAngularDiagramRenderer` (the narrated DI diagram),
  `ISourceFileReader` (the read-side I/O boundary, mirroring `IDiagramWriter`), and the
  `IAngularDiagramService` orchestrator. The `style` command adds `IPumlFileLocator` (file vs.
  recursive folder discovery), `ISequenceDiagramDetector` (conservative is-this-a-sequence-diagram
  classification), `ISequenceDiagramStyler` (the idempotent in-place restyle transform; shared
  comment/note-aware line scanning lives in the internal `PlantUmlScanner`), `ITextFileEditor`
  (the in-place read/write boundary that preserves the file's encoding and BOM), and the
  `IStyleService` orchestrator.
- `src/Cast.Cli/Models/` — immutable records (`Participant`, `Message`, `SequenceDiagram`,
  `ScaffoldRequest`, `ParticipantKind`, `DiagramStyle`; `AngularService`, `AngularDependency`,
  `AngularDiagramRequest`, `ConsumerKind`, `DependencyKind` for the `ng` command; `StyleRequest`,
  `StylerResult` for the `style` command).
- `src/Cast.Cli/Diagnostics/` — `DiagramFormatException` for user-facing input errors.

Conventions: both commands write a `.puml` file by default (`--stdout` prints the diagram to
**stdout** instead), and logs go to **stderr**. Adding a command means adding
an `ICliCommand` and registering it in `AddCast` — no central switchboard to edit.

PlantUML output conventions (both renderers): always emit `!pragma teoz true` and
`skinparam defaultFontSize 10`, and wrap the non-actor participants in a double nested box —
outer `#PHYSICAL`, inner `#AZURE` by default. Actors always stay outside the boxes. Both colors
are overridable per command via `--outer-box-color` / `--inner-box-color`, normalized and
validated by `DiagramStyle.FromOptions` (a missing `#` prefix is added).

## Tests

`tests/Cast.Cli.Tests/` — xUnit (`xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`). The
`Xunit` namespace is globally imported via a `<Using>` item, so test files don't need
`using Xunit;`. One test class per production type under test.
