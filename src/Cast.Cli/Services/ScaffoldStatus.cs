namespace Cast.Cli.Services;

/// <summary>
/// The domain outcome of a scaffold operation. Kept free of any presentation concern: the
/// command/host layer is responsible for translating a status into a process exit code, so the
/// Services layer never depends on <c>Cast.Cli.Hosting</c>.
/// </summary>
public enum ScaffoldStatus
{
    /// <summary>The diagram was rendered and written successfully.</summary>
    Success,

    /// <summary>The input was malformed or inconsistent (a bad spec, duplicate alias, unknown endpoint, …).</summary>
    InvalidInput,

    /// <summary>The diagram could not be written to its destination.</summary>
    OutputError,
}
