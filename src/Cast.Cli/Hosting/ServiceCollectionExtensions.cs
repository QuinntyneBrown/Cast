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
        services.AddSingleton<ISequenceDiagramRenderer, PlantUmlSequenceRenderer>();
        services.AddSingleton<IDiagramWriter>(_ => new FileSystemDiagramWriter());
        services.AddSingleton<IScaffoldService, ScaffoldService>();

        // Commands — every ICliCommand is discovered by RootCommandFactory.
        services.AddSingleton<ICliCommand, GenerateCommand>();
        services.AddSingleton<ICliCommand, ListKindsCommand>();

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
