# Cast

> A command-line tool for scaffolding [PlantUML](https://plantuml.com/) sequence diagrams.

[![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![Build](https://github.com/quinntyne/cast/actions/workflows/ci.yml/badge.svg)](https://github.com/quinntyne/cast/actions)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

Cast generates PlantUML sequence diagram scaffolding from the command line, so you can
go from an idea to a well-structured `.puml` diagram without hand-writing boilerplate.

> [!NOTE]
> **Early development.** Cast is in its initial scaffolding phase. The CLI surface and
> behaviour described below are the project's direction and are not all implemented yet.
> Expect breaking changes until the first tagged release.

## Requirements

- [.NET SDK 10.0](https://dotnet.microsoft.com/download) or later

## Getting started

Clone the repository and build:

```pwsh
git clone https://github.com/quinntyne/cast.git
cd cast
dotnet build
```

Run the CLI:

```pwsh
dotnet run --project src/Cast.Cli
```

## Development

```pwsh
dotnet build                              # build all projects
dotnet run --project src/Cast.Cli         # run the CLI
dotnet test                               # run the test suite
```

Run a single test:

```pwsh
dotnet test --filter "FullyQualifiedName~Cast.Cli.Tests.UnitTest1.Test1"
```

### Project layout

| Path                       | Description                              |
| -------------------------- | ---------------------------------------- |
| `src/Cast.Cli/`            | Console application and CLI entry point  |
| `tests/Cast.Cli.Tests/`    | xUnit test project                       |

The codebase targets **.NET 10** with nullable reference types and implicit usings enabled.

## Contributing

Contributions are welcome. To propose a change:

1. Fork the repository and create a feature branch.
2. Make your change, keeping it covered by tests (`dotnet test`).
3. Open a pull request describing the motivation and approach.

Please keep pull requests focused and ensure the build and tests pass before submitting.

## License

Distributed under the MIT License. See [`LICENSE`](LICENSE) for details.
