# Cast.Core

Cast.Core is the reusable .NET engine behind the Cast command-line tool. It provides dependency-free APIs for building and validating sequence diagrams, inspecting Angular and TypeScript source, rendering PlantUML, and applying Cast's sequence-diagram style.

The package targets .NET 8 and later. It does not depend on `System.CommandLine`, dependency injection, logging, filesystem adapters, or platform-specific editor integration.

## Install

```pwsh
dotnet add package Cast.Core --version 1.0.0
```

## Build and render a diagram

```csharp
using Cast.Core.Models;
using Cast.Core.Services;

var kinds = new ParticipantKindCatalog();
var participants = new ParticipantParser(kinds);
var messages = new MessageParser();
var validator = new DiagramSpecValidator(participants, messages);

IReadOnlyList<Participant> parsedParticipants = validator.ParseParticipants(
    ["actor:User", "control:Api:Ordering API"]);
IReadOnlyList<Message> parsedMessages = validator.ParseMessages(
    ["User -> Api : place order"],
    parsedParticipants);

var diagram = new SequenceDiagram(
    parsedParticipants,
    parsedMessages,
    Title: "Place an order");

var renderer = new PlantUmlSequenceRenderer(kinds);
string plantUml = renderer.Render(diagram);
```

Invalid participant, message, metadata, or source input throws `Cast.Core.Diagnostics.DiagramFormatException` with a caller-safe message.

## Inspect source code

Parse an Angular dependency graph directly from TypeScript source:

```csharp
using Cast.Core.Services;

var parser = new AngularServiceParser();
var renderer = new PlantUmlAngularDiagramRenderer();

var service = parser.Parse(typeScriptSource, "orders.service.ts");
string plantUml = renderer.Render(service);
```

For outbound TypeScript calls, use `TypeScriptCallGraphParser` with `PlantUmlCallGraphRenderer`. For existing PlantUML text, use `SequenceDiagramDetector` and `PlantUmlSequenceStyler`.

Cast.Core works entirely with strings and domain models. Reading source files, writing diagrams, logging, and dependency-injection registration remain responsibilities of the consuming application.

## Build the package

From the repository root:

```pwsh
dotnet pack src/Cast.Core/Cast.Core.csproj --configuration Release
```

Publishing the resulting `.nupkg` is intentionally separate from the automatic Cast.Cli release workflow.
