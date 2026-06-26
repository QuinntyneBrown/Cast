# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Status

`cast` is a working CLI that scaffolds initial PlantUML sequence diagrams. The `generate`
command turns command-line participants and messages into a `@startuml … @enduml` skeleton,
writing `cast.puml` in the current directory by default (`--stdout` prints instead); the
`kinds` command lists the participant kinds; the `ng` command inspects an Angular `.ts` file and
renders a narrated diagram of how Angular injects dependencies into any `inject()`-using construct
(service, component, directive, pipe, interceptor, guard, resolver, or exported function), writing a
`.puml` beside the source by default (`--stdout` prints instead); the `calls` command (alias
`callgraph`) reads a TypeScript `.ts` file and renders a call-graph sequence diagram — for each
public method (or, for a class-less file, each exported function) it shows the calls that member
makes to itself and its collaborators, resolving each receiver from field/constructor/local types,
writing a `.puml` beside the source by default (`--stdout` prints instead; `--method <name>` focuses
on named members regardless of visibility, `--include-private` widens past public); the `style`
command retrofits the
house styling onto existing `.puml` sequence diagrams in place (one file, or a folder scanned
recursively), leaving non-sequence diagrams untouched; the `template` command persists named
diagram templates as JSON under `%APPDATA%\cast\templates` (full CRUD via `save`/`list`/`show`/
`delete` subcommands) and renders one through the scaffolding pipeline when invoked with `--name`,
render-time options overriding the stored values (`-m` replaces stored messages entirely); the
`explorer` command opens the templates folder in VS Code (created first when missing). Every
command that writes a `.puml` file opens it in Notepad by default on Windows (`--no-open`
suppresses; `--stdout` never opens). The design follows SOLID with
`Microsoft.Extensions.DependencyInjection` and a one-command-per-file layout. Solution: `Cast.sln`.

## Commands

All projects target **.NET 8** (`net8.0`) so the tool runs on the .NET 8 runtime and later, with
`ImplicitUsings` and `Nullable` enabled. The CLI project sets `TreatWarningsAsErrors=true`, so the
build must stay warning-clean.

```pwsh
dotnet build Cast.sln                                         # build all projects
dotnet run --project src/Cast.Cli -- generate -p actor:User -p OS   # writes cast.puml in the cwd
dotnet run --project src/Cast.Cli -- ng -s path/to/foo.service.ts   # writes foo.service.puml beside it
dotnet run --project src/Cast.Cli -- calls -s path/to/order.service.ts  # call-graph diagram beside it
dotnet run --project src/Cast.Cli -- style docs/diagrams            # restyles sequence .puml files in place
dotnet run --project src/Cast.Cli -- template save --name acme -p actor:User -p OS   # upsert a template
dotnet run --project src/Cast.Cli -- template --name acme -m "User -> OS : order"    # render it to cast.puml
dotnet run --project src/Cast.Cli -- explorer                                        # open the templates folder in VS Code
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
  `NgCommand`, `CallGraphCommand`, `StyleCommand`, `TemplateCommand`, `ExplorerCommand`). A command maps parsed
  options to a request
  record and delegates to an orchestrator service; no `System.CommandLine` type leaks into the
  core. `TemplateCommand` is a command family: the parent command's own action renders
  (`cast template --name X`), and `save`/`list`/`show`/`delete` are subcommands built inside the
  same class — the parent's `--name` is deliberately not `Required` so subcommand invocations
  aren't blocked, and each subcommand constructs its own `Option` instances.
- `src/Cast.Cli/Services/` — focused, interface-backed services: kind catalog, participant and
  message parsers, the shared `IDiagramSpecValidator` (duplicate-alias, message-endpoint, and
  title/theme rules used by both `generate` and template saving), sample-flow generator, renderer
  (`ISequenceDiagramRenderer`), writer (`IDiagramWriter`), the best-effort `IFileOpener`
  (`NotepadFileOpener`: fire-and-forget `notepad.exe`, never throws, skips on non-Windows), and
  the `IScaffoldService` orchestrator. The `ng` command adds its own set:
  `IAngularServiceParser` (a comment/string-aware scanner that extracts a consumer and its injected
  dependencies — no Node sidecar), `IAngularDiagramRenderer` (the narrated DI diagram),
  `ISourceFileReader` (the read-side I/O boundary, mirroring `IDiagramWriter`), and the
  `IAngularDiagramService` orchestrator. The comment/string/regex-aware scanning the TypeScript
  parsers share lives in the internal `TypeScriptScanner` (sanitiser plus balanced-delimiter
  helpers). The `calls` command adds `ITypeScriptCallGraphParser` (`TypeScriptCallGraphParser`: a
  focused scanner that picks the primary class — or, for a class-less file, the exported functions —
  builds a symbol table from fields/constructor-params/locals, and collects each member's outbound
  calls, classifying them as self/collaborator/construction), `ICallGraphRenderer`
  (`PlantUmlCallGraphRenderer`: one interaction per method), and the `ICallGraphService` orchestrator
  (which owns the member-selection policy — `--method`/`--include-private`). The `style` command
  adds `IPumlFileLocator` (file vs.
  recursive folder discovery), `ISequenceDiagramDetector` (conservative is-this-a-sequence-diagram
  classification), `ISequenceDiagramStyler` (the idempotent in-place restyle transform; shared
  comment/note-aware line scanning lives in the internal `PlantUmlScanner`), `ITextFileEditor`
  (the in-place read/write boundary that preserves the file's encoding and BOM), and the
  `IStyleService` orchestrator. The `template` command adds `ITemplateStore`
  (`FileSystemTemplateStore`: JSON persistence under `%APPDATA%\cast\templates` with
  whitelist-validated names — no traversal, no reserved device names) and the `ITemplateService`
  orchestrator (validates before persisting so a bad template can't be saved; rendering merges the
  stored template with render-time overrides into a `ScaffoldRequest` and delegates to
  `IScaffoldService`). The `explorer` command adds `IFolderOpener` (`VsCodeFolderOpener`: launches
  `code <folder>` via ShellExecute; unlike `IFileOpener` a failed launch throws, because opening
  is the command's whole purpose) and the `IExplorerService` orchestrator.
- `src/Cast.Cli/Models/` — immutable records (`Participant`, `Message`, `SequenceDiagram`,
  `ScaffoldRequest`, `ParticipantKind`, `DiagramStyle`; `AngularService`, `AngularDependency`,
  `AngularDiagramRequest`, `ConsumerKind`, `DependencyKind` for the `ng` command; `CallGraph`,
  `CallGraphMethod`, `MethodCall`, `CallGraphSubjectKind`, `CallKind`, `MethodVisibility`, and
  `CallGraphRequest` for the `calls` command; `StyleRequest`,
  `StylerResult` for the `style` command; `DiagramTemplate` — an init-property record, the JSON
  contract — and `RenderTemplateRequest` for the `template` command).
- `src/Cast.Cli/Diagnostics/` — `DiagramFormatException` for user-facing input errors.

Conventions: the generating commands (`generate`, `ng`, `template`) write a `.puml` file by
default (`--stdout` prints the diagram to **stdout** instead) and then open it in Notepad on
Windows (`--no-open` suppresses; a failed launch logs a warning but never fails the command), and
logs go to **stderr**. Adding a command means adding an `ICliCommand` and registering it in
`AddCast` — no central switchboard to edit.

PlantUML output conventions (all renderers): always emit `!pragma teoz true` and
`skinparam defaultFontSize 10`, color every lifeline declaration (participants and actors alike)
with the fixed house color `#63BEF2` (`DiagramStyle.ParticipantColor`, not overridable), and wrap
the non-actor participants in a double nested box — outer `#PHYSICAL`, inner `#AZURE` by default.
Actors always stay outside the boxes. The box colors are overridable per command via
`--outer-box-color` / `--inner-box-color`, normalized and validated by `DiagramStyle.FromOptions`
(a missing `#` prefix is added). The `style` command retrofits the participant color too, leaving
declarations that already carry a color untouched.

## Tests

`tests/Cast.Cli.Tests/` — xUnit (`xunit`, `Microsoft.NET.Test.Sdk`, `coverlet.collector`). The
`Xunit` namespace is globally imported via a `<Using>` item, so test files don't need
`using Xunit;`. One test class per production type under test.
