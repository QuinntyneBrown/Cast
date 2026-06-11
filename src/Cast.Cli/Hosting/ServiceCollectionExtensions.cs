using Cast.Cli.Commands;
using Cast.Cli.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;

namespace Cast.Cli.Hosting;

/// <summary>
/// The single composition root: registers every service, command, and the root-command factory.
/// Each abstraction is bound to its implementation here and nowhere else, which is what lets the
/// rest of the codebase depend only on interfaces (Dependency Inversion).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Registers all Cast services and CLI commands into <paramref name="services"/>.</summary>
    public static IServiceCollection AddCast(this IServiceCollection services)
    {
        AddLogging(services);

        // Core services — stateless, so singletons are safe and cheap.
        services.AddSingleton<IParticipantKindCatalog, ParticipantKindCatalog>();
        services.AddSingleton<IParticipantParser, ParticipantParser>();
        services.AddSingleton<IMessageParser, MessageParser>();
        services.AddSingleton<ISampleFlowGenerator, SequentialSampleFlowGenerator>();
        services.AddSingleton<IDiagramSpecValidator, DiagramSpecValidator>();
        services.AddSingleton<ISequenceDiagramRenderer, PlantUmlSequenceRenderer>();
        services.AddSingleton<IDiagramWriter>(_ => new FileSystemDiagramWriter());
        services.AddSingleton<IFileOpener, NotepadFileOpener>();
        services.AddSingleton<IScaffoldService, ScaffoldService>();

        // Angular DI inspection (the `ng` command): read a .ts file, extract its DI graph, render.
        services.AddSingleton<ISourceFileReader, FileSystemSourceReader>();
        services.AddSingleton<IAngularServiceParser, AngularServiceParser>();
        services.AddSingleton<IAngularDiagramRenderer, PlantUmlAngularDiagramRenderer>();
        services.AddSingleton<IAngularDiagramService, AngularDiagramService>();

        // Named templates (the `template` command): persist diagram definitions as JSON under the
        // per-user application-data folder and render them through the scaffolding pipeline.
        // The `explorer` command opens that folder in VS Code for hand editing.
        services.AddSingleton<ITemplateStore>(_ => new FileSystemTemplateStore());
        services.AddSingleton<ITemplateService, TemplateService>();
        services.AddSingleton<IFolderOpener, VsCodeFolderOpener>();
        services.AddSingleton<IExplorerService, ExplorerService>();

        // Restyling existing diagrams (the `style` command): locate .puml files, classify, rewrite
        // in place through the encoding-preserving editor.
        services.AddSingleton<IPumlFileLocator, FileSystemPumlFileLocator>();
        services.AddSingleton<ITextFileEditor, FileSystemTextFileEditor>();
        services.AddSingleton<ISequenceDiagramDetector, SequenceDiagramDetector>();
        services.AddSingleton<ISequenceDiagramStyler, PlantUmlSequenceStyler>();
        services.AddSingleton<IStyleService, StyleService>();

        // Commands — every ICliCommand is discovered by RootCommandFactory.
        services.AddSingleton<ICliCommand, GenerateCommand>();
        services.AddSingleton<ICliCommand, ListKindsCommand>();
        services.AddSingleton<ICliCommand, NgCommand>();
        services.AddSingleton<ICliCommand, StyleCommand>();
        services.AddSingleton<ICliCommand, TemplateCommand>();
        services.AddSingleton<ICliCommand, ExplorerCommand>();

        services.AddSingleton<RootCommandFactory>();

        return services;
    }

    private static void AddLogging(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Information);
            builder.AddSimpleConsole(options =>
            {
                options.SingleLine = true;
                options.IncludeScopes = false;
            });
        });

        // Diagrams go to standard output; route every log to standard error so a piped or
        // redirected stdout contains only PlantUML.
        services.Configure<ConsoleLoggerOptions>(
            options => options.LogToStandardErrorThreshold = LogLevel.Trace);
    }
}
