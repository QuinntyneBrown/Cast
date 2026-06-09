# Cast

> A command-line tool for scaffolding [PlantUML](https://plantuml.com/) sequence diagrams.

[![.NET](https://img.shields.io/badge/.NET-8.0%2B-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![NuGet](https://img.shields.io/nuget/v/Cast.Cli.svg)](https://www.nuget.org/packages/Cast.Cli/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Cast turns command-line participants and messages into a PlantUML sequence diagram starter.

## Requirements

- [.NET SDK 8.0](https://dotnet.microsoft.com/download) or later (the tool targets `net8.0` and runs on the .NET 8 runtime and later)

## Install From NuGet

Install the latest published `Cast.Cli` tool package from NuGet:

```pwsh
dotnet tool install --global Cast.Cli
```

Or pin the currently published latest version:

```pwsh
dotnet tool install --global Cast.Cli --version 1.0.8
```

After installation, run Cast with the `cast` command:

```pwsh
cast --help
cast generate -p actor:User -p OS -m "User -> OS : place order"
cast ng --service projects/api/src/lib/services/dashboard.service.ts
```

Update an existing global install:

```pwsh
dotnet tool update --global Cast.Cli
```

For a repository-local tool install:

```pwsh
dotnet new tool-manifest
dotnet tool install Cast.Cli
dotnet tool run cast -- --help
```

## Build And Test

```pwsh
git clone https://github.com/QuinntyneBrown/Cast.git
cd Cast
dotnet build Cast.sln
dotnet test Cast.sln
```

## Run From Source

Use `dotnet run --project src/Cast.Cli --` followed by a Cast command:

```pwsh
dotnet run --project src/Cast.Cli -- generate `
  -p actor:User `
  -p "OS:Order Service" `
  -p database:DB `
  -m "User -> OS : place order" `
  -m "OS -> DB : persist" `
  --title "Checkout" `
  --autonumber `
  --stdout
```

By default the diagram is written to `cast.puml` in the current directory; pass `--stdout` to
print it to standard output instead, or `-o <file>` to choose another path.

Output:

```plantuml
@startuml
' Scaffolded by cast
title Checkout
autonumber

actor User
participant "Order Service" as OS
database DB

User -> OS : place order
OS -> DB : persist
@enduml
```

## Commands

### `generate`

Scaffolds a PlantUML sequence diagram from participants and optional messages.

| Option | Description |
| --- | --- |
| `-p, --participant <spec>` | Required, repeatable. Participant spec: `[kind:]alias[:Display Name]`. |
| `-m, --message <spec>` | Repeatable. Message spec: `Source -> Target : label`. |
| `-t, --title <text>` | Adds a PlantUML `title`. |
| `--autonumber` | Adds PlantUML `autonumber`. |
| `--theme <name>` | Adds PlantUML `!theme <name>`. |
| `-o, --output <file>` | Write to this file. Defaults to `cast.puml` in the current directory. |
| `--stdout` | Write to standard output instead of a file (takes precedence over `--output`). |
| `--force` | Overwrites an existing output file. |
| `--no-sample` | Disables placeholder messages when no `--message` values are supplied. |

Participant examples:

```text
User
actor:Customer
database:DB:Main Database
```

Message examples:

```text
User -> OS : place order
OS --> User : confirmation
```

When participants are supplied without messages, Cast generates a placeholder request/response flow
unless `--no-sample` is used.

### `kinds`

Lists supported participant kind prefixes:

```pwsh
dotnet run --project src/Cast.Cli -- kinds
```

Supported kinds are `participant`, `actor`, `boundary`, `control`, `entity`, `database`,
`collections`, and `queue`.

### `ng`

Inspects an Angular `.ts` file and emits a PlantUML sequence diagram explaining how Angular's
dependency injector supplies that consumer's dependencies. The consumer can be **any construct that
uses `inject(...)`** — an `@Injectable` service, an `@Component`/`@Directive`/`@Pipe`, a functional
interceptor/guard/resolver, or an exported function (a factory or initializer). It recognises both
idiomatic DI shapes: the `inject(X)` function (in class fields, functional providers, and function
declarations) and classic constructor injection (including `@Inject(TOKEN)` and `@Optional()`).
Symbols written in `SCREAMING_CASE` or injected via `@Inject` are treated as `InjectionToken`s;
PascalCase symbols such as `HttpClient` or `Router` are treated as classes the injector constructs.
Parsing is done in pure .NET — no Node runtime is required.

| Option | Description |
| --- | --- |
| `-s, --service, --file <file>` | Required. Path to the Angular `.ts` file to inspect. |
| `-t, --title <text>` | Override the generated diagram title. |
| `-o, --output <file>` | Write to this file. Defaults to the source path with a `.puml` extension (`foo.service.ts` → `foo.service.puml`). |
| `--stdout` | Write to standard output instead of a file (takes precedence over `--output`). |
| `--force` | Overwrite an existing output file. |

By default the diagram is written next to the source as a `.puml` file; pass `--stdout` to print it
to standard output instead.

```pwsh
dotnet run --project src/Cast.Cli -- ng `
  --service projects/api/src/lib/services/dashboard.service.ts
```

For an `@Injectable({ providedIn: 'root' })` service that injects `HttpClient` and the
`API_BASE_URL` token, Cast produces a narrated diagram:

```plantuml
@startuml
' Generated by cast ng
title How Angular injects dependencies into DashboardService

actor "App\n(a component or service\nthat needs DashboardService)" as App
participant "Angular Injector\n(the built-in DI container)" as DI
participant "DashboardService\n(the consumer)" as Consumer
participant "HttpClient\n(injected service)" as D1
participant "API_BASE_URL\n(injected token)" as D2

App -> DI : Give me a DashboardService
note over DI
  Angular inspects DashboardService and sees it needs 2 dependencies:
  - HttpClient (service)
  - API_BASE_URL (token)
end note
DI -> D1 : resolve HttpClient
DI -> D2 : look up API_BASE_URL
DI -> Consumer : create DashboardService
DI --> App : a ready-to-use DashboardService\n(dependencies already wired inside)
@enduml
```

A functional provider (an interceptor, guard, or resolver) is rendered with the "pull" narrative
it actually follows — the function runs inside an injection context and calls `inject(...)` for each
dependency it needs.

## Exit Codes

| Code | Meaning |
| --- | --- |
| `0` | Success |
| `1` | Usage error, such as malformed input or an unknown message endpoint |
| `2` | I/O error, such as an existing output file without `--force` |

## Project Layout

| Path | Description |
| --- | --- |
| `src/Cast.Cli/` | CLI commands, hosting, models, diagnostics, and services |
| `tests/Cast.Cli.Tests/` | xUnit tests |

## License

Distributed under the MIT License. See [LICENSE](LICENSE) for details.
