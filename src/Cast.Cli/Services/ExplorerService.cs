using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Cast.Cli.Services;

/// <summary>
/// Default <see cref="IExplorerService"/>. Asks the store for its (created-if-missing) templates
/// folder and hands it to the folder opener; a store or launch failure is reported as
/// <see cref="ScaffoldStatus.OutputError"/>.
/// </summary>
public sealed class ExplorerService : IExplorerService
{
    private readonly ITemplateStore _store;
    private readonly IFolderOpener _folderOpener;
    private readonly ILogger<ExplorerService> _logger;

    public ExplorerService(ITemplateStore store, IFolderOpener folderOpener, ILogger<ExplorerService> logger)
    {
        _store = store;
        _folderOpener = folderOpener;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ScaffoldStatus> ExecuteAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string root = _store.EnsureRootDirectory();
            _folderOpener.Open(root);

            _logger.LogInformation("Opened the templates folder {Root} in Visual Studio Code.", root);
            return Task.FromResult(ScaffoldStatus.Success);
        }
        catch (IOException ex)
        {
            _logger.LogError("{Message}", ex.Message);
            return Task.FromResult(ScaffoldStatus.OutputError);
        }
    }
}
