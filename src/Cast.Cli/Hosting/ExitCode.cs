namespace Cast.Cli.Hosting;

/// <summary>
/// Process exit codes returned by commands, following the usual convention of
/// <c>0 == success</c> and distinct non-zero codes for distinguishable failure classes.
/// </summary>
public static class ExitCode
{
    /// <summary>The command completed successfully.</summary>
    public const int Success = 0;

    /// <summary>The user supplied malformed or inconsistent input (a bad spec, duplicate alias, …).</summary>
    public const int UsageError = 1;

    /// <summary>The diagram could not be written to its destination.</summary>
    public const int IoError = 2;
}
